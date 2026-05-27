using System.Text.Json;
using Cronos;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Reports;

public sealed class OperationalReportFilters
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? BusinessDate { get; set; }
    public Guid? CashRegisterId { get; set; }
}

public interface IOperationalReportScheduler
{
    DateTime? ComputeNextRunUtc(string cronExpression, DateTime? fromUtc = null);

    Task<OperationalReportSchedule> CreateScheduleAsync(
        Guid tenantId,
        string createdByUserId,
        AdminOperationalReportType reportType,
        string scheduleCron,
        IReadOnlyList<string> recipients,
        string format,
        OperationalReportFilters filters,
        CancellationToken cancellationToken = default);

    Task RunDueSchedulesAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);
}

public sealed class OperationalReportScheduler : IOperationalReportScheduler
{
    private readonly AppDbContext _context;
    private readonly IAdminOperationalReportExportService _exportService;
    private readonly IAuditReportEmailService _emailService;
    private readonly ILogger<OperationalReportScheduler> _logger;

    public OperationalReportScheduler(
        AppDbContext context,
        IAdminOperationalReportExportService exportService,
        IAuditReportEmailService emailService,
        ILogger<OperationalReportScheduler> logger)
    {
        _context = context;
        _exportService = exportService;
        _emailService = emailService;
        _logger = logger;
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

    public async Task<OperationalReportSchedule> CreateScheduleAsync(
        Guid tenantId,
        string createdByUserId,
        AdminOperationalReportType reportType,
        string scheduleCron,
        IReadOnlyList<string> recipients,
        string format,
        OperationalReportFilters filters,
        CancellationToken cancellationToken = default)
    {
        var next = ComputeNextRunUtc(scheduleCron);
        if (!next.HasValue)
            throw new ArgumentException("Invalid cron schedule expression.", nameof(scheduleCron));

        var schedule = new OperationalReportSchedule
        {
            TenantId = tenantId,
            CreatedByUserId = createdByUserId,
            ReportType = reportType.ToString(),
            ScheduleCron = scheduleCron.Trim(),
            RecipientsJson = JsonSerializer.Serialize(recipients),
            Format = format.Trim().ToLowerInvariant(),
            FiltersJson = JsonSerializer.Serialize(filters),
            IsActive = true,
            NextRunUtc = next,
        };

        _context.OperationalReportSchedules.Add(schedule);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return schedule;
    }

    public async Task RunDueSchedulesAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var query = _context.OperationalReportSchedules
            .Where(s => s.IsActive && s.NextRunUtc != null && s.NextRunUtc <= now);
        if (tenantId.HasValue)
            query = query.Where(s => s.TenantId == tenantId.Value);

        var due = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var schedule in due)
        {
            try
            {
                await RunSingleAsync(schedule, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Operational report schedule {Id} failed", schedule.Id);
            }
        }
    }

    private async Task RunSingleAsync(OperationalReportSchedule schedule, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AdminOperationalReportType>(schedule.ReportType, ignoreCase: true, out var reportType))
        {
            _logger.LogWarning("Unknown report type {Type} on schedule {Id}", schedule.ReportType, schedule.Id);
            return;
        }

        var filters = JsonSerializer.Deserialize<OperationalReportFilters>(schedule.FiltersJson) ?? new OperationalReportFilters();
        var recipients = JsonSerializer.Deserialize<List<string>>(schedule.RecipientsJson) ?? new List<string>();
        recipients = recipients.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).ToList();
        if (recipients.Count == 0)
            return;

        var (bytes, contentType, fileName) = await _exportService.ExportAsync(
            reportType,
            schedule.Format,
            filters.StartDate,
            filters.EndDate,
            filters.BusinessDate,
            filters.CashRegisterId,
            cancellationToken).ConfigureAwait(false);

        if (_emailService.IsConfigured)
        {
            await _emailService.SendReportAsync(
                recipients,
                $"Regkasse Report: {reportType}",
                $"Scheduled operational report ({schedule.Format}). Generated at {DateTime.UtcNow:u} UTC.",
                fileName,
                bytes,
                contentType,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogWarning("SMTP not configured; operational schedule {Id} not emailed.", schedule.Id);
        }

        schedule.LastRunUtc = DateTime.UtcNow;
        schedule.NextRunUtc = ComputeNextRunUtc(schedule.ScheduleCron, schedule.LastRunUtc);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class OperationalReportSchedulerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OperationalReportSchedulerHostedService> _logger;

    public OperationalReportSchedulerHostedService(IServiceScopeFactory scopeFactory, ILogger<OperationalReportSchedulerHostedService> logger)
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
                var scheduler = scope.ServiceProvider.GetRequiredService<IOperationalReportScheduler>();
                var accessor = scope.ServiceProvider.GetRequiredService<ICurrentTenantAccessor>();
                var tenantIds = await db.OperationalReportSchedules
                    .Where(s => s.IsActive && s.NextRunUtc != null && s.NextRunUtc <= DateTime.UtcNow)
                    .Select(s => s.TenantId)
                    .Distinct()
                    .ToListAsync(stoppingToken)
                    .ConfigureAwait(false);

                foreach (var tenantId in tenantIds)
                {
                    accessor.TenantId = tenantId;
                    await scheduler.RunDueSchedulesAsync(tenantId, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Operational report scheduler tick failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
        }
    }
}
