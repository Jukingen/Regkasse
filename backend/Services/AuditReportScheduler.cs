using System.Text.Json;
using Cronos;
using KasseAPI_Final;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IAuditReportScheduler
{
    DateTime? ComputeNextRunUtc(string cronExpression, DateTime? fromUtc = null);

    Task<IReadOnlyList<AuditReportSchedule>> GetSchedulesAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<AuditReportSchedule?> GetScheduleByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> DeactivateScheduleAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AuditReportSchedule> CreateScheduleAsync(
        Guid tenantId,
        string createdByUserId,
        string name,
        AuditLogQueryFilters filters,
        string scheduleCron,
        IReadOnlyList<string> recipients,
        string format,
        CancellationToken cancellationToken = default);

    Task RunDueSchedulesAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);
}

public sealed class AuditReportScheduler : IAuditReportScheduler
{
    private readonly AppDbContext _context;
    private readonly IAuditExportService _exportService;
    private readonly IAuditReportEmailService _emailService;
    private readonly ILogger<AuditReportScheduler> _logger;

    public AuditReportScheduler(
        AppDbContext context,
        IAuditExportService exportService,
        IAuditReportEmailService emailService,
        ILogger<AuditReportScheduler> logger)
    {
        _context = context;
        _exportService = exportService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AuditReportSchedule>> GetSchedulesAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await _context.AuditReportSchedules
            .Where(s => s.TenantId == tenantId && s.IsActive)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<AuditReportSchedule?> GetScheduleByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.AuditReportSchedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<bool> DeactivateScheduleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var schedule = await _context.AuditReportSchedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken).ConfigureAwait(false);
        if (schedule == null)
            return false;
        schedule.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public DateTime? ComputeNextRunUtc(string cronExpression, DateTime? fromUtc = null)
    {
        try
        {
            var expr = CronExpression.Parse(cronExpression, CronFormat.Standard);
            return expr.GetNextOccurrence(fromUtc ?? DateTime.UtcNow, TimeZoneInfo.Utc, inclusive: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid cron expression: {Cron}", cronExpression);
            return null;
        }
    }

    public async Task<AuditReportSchedule> CreateScheduleAsync(
        Guid tenantId,
        string createdByUserId,
        string name,
        AuditLogQueryFilters filters,
        string scheduleCron,
        IReadOnlyList<string> recipients,
        string format,
        CancellationToken cancellationToken = default)
    {
        var next = ComputeNextRunUtc(scheduleCron);
        if (!next.HasValue)
            throw new ArgumentException("Invalid cron schedule expression.", nameof(scheduleCron));

        var schedule = new AuditReportSchedule
        {
            TenantId = tenantId,
            CreatedByUserId = createdByUserId,
            Name = name.Trim(),
            FiltersJson = JsonSerializer.Serialize(filters),
            ScheduleCron = scheduleCron.Trim(),
            RecipientsJson = JsonSerializer.Serialize(recipients),
            Format = format.Trim().ToLowerInvariant(),
            IsActive = true,
            NextRunUtc = next,
        };

        _context.AuditReportSchedules.Add(schedule);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return schedule;
    }

    public async Task RunDueSchedulesAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var query = _context.AuditReportSchedules
            .Where(s => s.IsActive && s.NextRunUtc != null && s.NextRunUtc <= now);
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
                _logger.LogError(ex, "Scheduled audit report {ScheduleId} failed", schedule.Id);
            }
        }
    }

    private async Task RunSingleScheduleAsync(AuditReportSchedule schedule, CancellationToken cancellationToken)
    {
        var filters = JsonSerializer.Deserialize<AuditLogQueryFilters>(schedule.FiltersJson) ?? new AuditLogQueryFilters();
        var recipients = JsonSerializer.Deserialize<List<string>>(schedule.RecipientsJson) ?? new List<string>();
        recipients = recipients.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).ToList();
        if (recipients.Count == 0)
        {
            _logger.LogWarning("Audit schedule {Id} has no recipients; skipping.", schedule.Id);
            return;
        }

        var bytes = await _exportService.ExportToBytesAsync(filters, schedule.Format, cancellationToken).ConfigureAwait(false);
        var ext = schedule.Format == "json" ? "json" : "csv";
        var fileName = $"audit_report_{schedule.Name.Replace(' ', '_')}_{DateTime.UtcNow:yyyyMMdd}.{ext}";
        var contentType = ext == "json" ? "application/json" : "text/csv";

        if (_emailService.IsConfigured)
        {
            await _emailService.SendReportAsync(
                recipients,
                $"Regkasse Audit Report: {schedule.Name}",
                $"Scheduled audit report \"{schedule.Name}\" ({schedule.Format}). Generated at {DateTime.UtcNow:u} UTC.",
                fileName,
                bytes,
                contentType,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogWarning("SMTP not configured; audit schedule {Id} export generated but not emailed.", schedule.Id);
        }

        schedule.LastRunUtc = DateTime.UtcNow;
        schedule.NextRunUtc = ComputeNextRunUtc(schedule.ScheduleCron, schedule.LastRunUtc);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Runs tenant audit report schedules when due.</summary>
public sealed class AuditReportSchedulerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditReportSchedulerHostedService> _logger;

    public AuditReportSchedulerHostedService(IServiceScopeFactory scopeFactory, ILogger<AuditReportSchedulerHostedService> logger)
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
                var schedules = scope.ServiceProvider.GetRequiredService<IAuditReportScheduler>();
                var tenants = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var dueTenantIds = await tenants.AuditReportSchedules
                    .Where(s => s.IsActive && s.NextRunUtc != null && s.NextRunUtc <= DateTime.UtcNow)
                    .Select(s => s.TenantId)
                    .Distinct()
                    .ToListAsync(stoppingToken)
                    .ConfigureAwait(false);

                var accessor = scope.ServiceProvider.GetRequiredService<ICurrentTenantAccessor>();
                foreach (var tenantId in dueTenantIds)
                {
                    accessor.TenantId = tenantId;
                    await schedules.RunDueSchedulesAsync(tenantId, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Audit report scheduler tick failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
        }
    }
}
