using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IDepExportScheduler
{
    DateTime ComputeNextRunUtc(
        string scheduleType,
        int dayOfMonth,
        string timeOfDay,
        DateTime? fromUtc = null);

    Task<IReadOnlyList<DepExportSchedule>> GetSchedulesAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<DepExportSchedule?> GetScheduleByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> DeactivateScheduleAsync(Guid id, CancellationToken cancellationToken = default);

    Task<DepExportSchedule> CreateScheduleAsync(
        Guid tenantId,
        Guid cashRegisterId,
        string scheduleType,
        int dayOfMonth,
        string timeOfDay,
        string? recipientEmails,
        CancellationToken cancellationToken = default);

    Task RunDueSchedulesAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);
}

public sealed class DepExportScheduler : IDepExportScheduler
{
    private readonly AppDbContext _context;
    private readonly IRksvDepExportService _depExportService;
    private readonly IDepExportHistoryService _historyService;
    private readonly IAuditReportEmailService _emailService;
    private readonly ILogger<DepExportScheduler> _logger;
    private readonly string _storageRoot;

    public DepExportScheduler(
        AppDbContext context,
        IRksvDepExportService depExportService,
        IDepExportHistoryService historyService,
        IAuditReportEmailService emailService,
        ILogger<DepExportScheduler> logger)
    {
        _context = context;
        _depExportService = depExportService;
        _historyService = historyService;
        _emailService = emailService;
        _logger = logger;
        _storageRoot = Path.Combine(Path.GetTempPath(), "regkasse-dep-exports");
        Directory.CreateDirectory(_storageRoot);
    }

    public DateTime ComputeNextRunUtc(
        string scheduleType,
        int dayOfMonth,
        string timeOfDay,
        DateTime? fromUtc = null) =>
        DepExportScheduleTiming.ComputeNextRunUtc(scheduleType, dayOfMonth, timeOfDay, fromUtc);

    public async Task<IReadOnlyList<DepExportSchedule>> GetSchedulesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await _context.DepExportSchedules
            .Where(s => s.TenantId == tenantId && s.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<DepExportSchedule?> GetScheduleByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.DepExportSchedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<bool> DeactivateScheduleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var schedule = await _context.DepExportSchedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken).ConfigureAwait(false);
        if (schedule is null)
            return false;
        schedule.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<DepExportSchedule> CreateScheduleAsync(
        Guid tenantId,
        Guid cashRegisterId,
        string scheduleType,
        int dayOfMonth,
        string timeOfDay,
        string? recipientEmails,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = DepExportScheduleTypes.Normalize(scheduleType);
        _ = DepExportScheduleTiming.ParseTimeOfDay(timeOfDay);

        if (dayOfMonth is < 1 or > 31)
            throw new ArgumentException("dayOfMonth must be between 1 and 31.", nameof(dayOfMonth));

        var registerExists = await _context.CashRegisters
            .AnyAsync(c => c.Id == cashRegisterId, cancellationToken)
            .ConfigureAwait(false);
        if (!registerExists)
            throw new InvalidOperationException($"Cash register {cashRegisterId} not found.");

        var schedule = new DepExportSchedule
        {
            TenantId = tenantId,
            CashRegisterId = cashRegisterId,
            ScheduleType = normalizedType,
            DayOfMonth = dayOfMonth,
            TimeOfDay = timeOfDay.Trim(),
            RecipientEmails = string.IsNullOrWhiteSpace(recipientEmails) ? null : recipientEmails.Trim(),
            IsActive = true,
            NextRunAt = ComputeNextRunUtc(normalizedType, dayOfMonth, timeOfDay),
        };

        _context.DepExportSchedules.Add(schedule);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return schedule;
    }

    public async Task RunDueSchedulesAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var query = _context.DepExportSchedules
            .Where(s => s.IsActive && s.NextRunAt != null && s.NextRunAt <= now);
        if (tenantId.HasValue)
            query = query.Where(s => s.TenantId == tenantId.Value);

        var due = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var schedule in due)
        {
            try
            {
                await RunSingleScheduleAsync(schedule, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled DEP export {ScheduleId} failed", schedule.Id);
            }
        }
    }

    private async Task RunSingleScheduleAsync(DepExportSchedule schedule, CancellationToken cancellationToken)
    {
        var runAt = DateTime.UtcNow;
        var (fromUtc, toUtc) = DepExportScheduleTiming.ResolveExportWindow(schedule.ScheduleType, runAt);
        var recipients = DepExportScheduleTiming.ParseRecipientEmails(schedule.RecipientEmails);

        try
        {
            var export = await _depExportService.GenerateDepExportAsync(
                    schedule.CashRegisterId,
                    fromUtc,
                    toUtc,
                    includeSpecialReceipts: true,
                    includeDailyClosings: true,
                    cancellationToken)
                .ConfigureAwait(false);

            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
            var fileName = DepExportHistoryService.BuildFileName(schedule.CashRegisterId, fromUtc, toUtc);
            var storagePath = Path.Combine(_storageRoot, $"{schedule.TenantId:N}_{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(storagePath, json, cancellationToken).ConfigureAwait(false);

            await _historyService.RecordCompletedAsync(
                    new DepExportHistoryRecordRequest
                    {
                        TenantId = schedule.TenantId,
                        CashRegisterId = schedule.CashRegisterId,
                        FromUtc = fromUtc,
                        ToUtc = toUtc,
                        ExportedByUserId = "system-scheduler",
                        Export = export,
                        ScheduleId = schedule.Id,
                        StoragePath = storagePath,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (recipients.Count > 0 && _emailService.IsConfigured)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                await _emailService.SendReportAsync(
                        recipients,
                        $"Regkasse DEP Export ({schedule.ScheduleType})",
                        $"Scheduled DEP export ({schedule.ScheduleType}) for register {schedule.CashRegisterId}. Generated at {runAt:u} UTC.",
                        fileName,
                        bytes,
                        "application/json",
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (recipients.Count > 0)
            {
                _logger.LogWarning("SMTP not configured; DEP schedule {Id} export stored but not emailed.", schedule.Id);
            }
        }
        catch (Exception ex)
        {
            await _historyService.RecordFailedAsync(
                    schedule.TenantId,
                    schedule.CashRegisterId,
                    fromUtc,
                    toUtc,
                    "system-scheduler",
                    ex.Message,
                    scheduleId: schedule.Id,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            throw;
        }

        schedule.LastRunAt = runAt;
        schedule.NextRunAt = ComputeNextRunUtc(
            schedule.ScheduleType,
            schedule.DayOfMonth,
            schedule.TimeOfDay,
            runAt);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Runs tenant DEP export schedules when due.</summary>
public sealed class DepExportSchedulerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DepExportSchedulerHostedService> _logger;

    public DepExportSchedulerHostedService(IServiceScopeFactory scopeFactory, ILogger<DepExportSchedulerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (OpenApiExportMode.IsEnabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var scheduler = scope.ServiceProvider.GetRequiredService<IDepExportScheduler>();
                // Cross-tenant due discovery: ambient tenant is unset. Keep IgnoreQueryFilters if schedules
                // ever become ITenantEntity (fail-closed filter would otherwise return no rows).
                var dueTenantIds = await db.DepExportSchedules
                    .IgnoreQueryFilters()
                    .Where(s => s.IsActive && s.NextRunAt != null && s.NextRunAt <= DateTime.UtcNow)
                    .Select(s => s.TenantId)
                    .Distinct()
                    .ToListAsync(stoppingToken)
                    .ConfigureAwait(false);

                var accessor = scope.ServiceProvider.GetRequiredService<ICurrentTenantAccessor>();
                foreach (var tenantId in dueTenantIds)
                {
                    accessor.TenantId = tenantId;
                    await scheduler.RunDueSchedulesAsync(tenantId, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "DEP export scheduler tick failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
        }
    }
}
