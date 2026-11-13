using Cronos;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Admin-facing per-tenant backup automation (cron + retention).</summary>
public sealed class BackupSettingsAdminService : IBackupSettingsAdminService
{
    public const int MinRetentionDays = 7;
    public const int MaxRetentionDays = 90;

    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ILogger<BackupSettingsAdminService> _logger;

    public BackupSettingsAdminService(
        AppDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        ILogger<BackupSettingsAdminService> logger)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    public async Task<BackupSettingsResponseDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var row = await GetTenantRowTrackedAsync(requireTenant: true, cancellationToken);
        return Map(row);
    }

    public async Task<BackupSettingsResponseDto> PutAsync(
        BackupSettingsPutRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        string cron;
        if (dto.Schedule != null)
        {
            try
            {
                cron = BackupScheduleCronCodec.BuildCron(dto.Schedule);
            }
            catch (ArgumentException ex) when (ex.Message is "INVALID_CRON_EXPRESSION")
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ArgumentException(ex.Message, nameof(dto.Schedule), ex);
            }
        }
        else
        {
            cron = (dto.ScheduleCron ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(cron) || !CronExpression.TryParse(cron, CronFormat.Standard, out _))
                throw new ArgumentException("INVALID_CRON_EXPRESSION", nameof(dto));
        }

        if (dto.RetentionDays < MinRetentionDays || dto.RetentionDays > MaxRetentionDays)
            throw new ArgumentOutOfRangeException(nameof(dto), dto.RetentionDays,
                $"RetentionDays must be between {MinRetentionDays} and {MaxRetentionDays}.");

        var row = await GetTenantRowTrackedAsync(requireTenant: true, cancellationToken);

        row.Enabled = dto.Enabled;
        row.ScheduleCron = cron;
        row.RetentionDays = dto.RetentionDays;
        var utcNow = DateTime.UtcNow;
        row.UpdatedAt = utcNow;
        BackupScheduleProjectionHelper.RefreshNextRunAt(row, utcNow);

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Tenant backup schedule updated: TenantId={TenantId}, Enabled={Enabled}, RetentionDays={RetentionDays}",
            row.TenantId,
            row.Enabled,
            row.RetentionDays);
        return Map(row);
    }

    public async Task<BackupScheduleStatusResponseDto> GetScheduleStatusAsync(CancellationToken cancellationToken = default)
    {
        var row = await GetTenantRowReadOnlyAsync(cancellationToken);

        BackupScheduleLatestRunSummaryDto? lastScheduled = null;
        var latestScheduled = await _db.BackupRuns.AsNoTracking()
            .Where(r => r.TriggerSource == BackupTriggerSource.Scheduled)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestScheduled != null)
        {
            lastScheduled = new BackupScheduleLatestRunSummaryDto
            {
                Id = latestScheduled.Id,
                Status = latestScheduled.Status,
                RequestedAt = latestScheduled.RequestedAt,
                CompletedAt = latestScheduled.CompletedAt,
                FailureCode = latestScheduled.FailureCode,
                FailureDetail = latestScheduled.FailureDetail
            };
        }

        DateTime? computedNextUtc = null;
        if (row.Enabled
            && !string.IsNullOrWhiteSpace(row.ScheduleCron)
            && CronExpression.TryParse(row.ScheduleCron.Trim(), CronFormat.Standard, out var expr))
            computedNextUtc = expr.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc, inclusive: false);

        return new BackupScheduleStatusResponseDto
        {
            DatabaseAutomationEnabled = row.Enabled,
            ScheduleCronUtc = row.ScheduleCron,
            StoredLastRunAtUtc = row.LastRunAt,
            StoredNextRunAtUtc = row.NextRunAt,
            ComputedNextRunAtUtc = computedNextUtc,
            LatestScheduledBackupRun = lastScheduled
        };
    }

    private async Task<BackupScheduleConfiguration> GetTenantRowTrackedAsync(
        bool requireTenant,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (!tenantId.HasValue)
        {
            if (requireTenant)
                throw new InvalidOperationException("TENANT_CONTEXT_REQUIRED");
            throw new InvalidOperationException("Tenant context is required for backup schedule settings.");
        }

        return await BackupScheduleConfigurationEnsure.EnsureForTenantAsync(_db, tenantId.Value, cancellationToken);
    }

    private async Task<BackupScheduleConfiguration> GetTenantRowReadOnlyAsync(CancellationToken cancellationToken)
    {
        var tenantId = _tenantAccessor.TenantId
            ?? throw new InvalidOperationException("TENANT_CONTEXT_REQUIRED");

        await BackupScheduleConfigurationEnsure.EnsureForTenantAsync(_db, tenantId, cancellationToken);
        return await _db.BackupScheduleConfigurations.AsNoTracking()
            .FirstAsync(x => x.TenantId == tenantId, cancellationToken);
    }

    private static BackupSettingsResponseDto Map(BackupScheduleConfiguration row)
    {
        BackupScheduleCronCodec.TryParseCron(row.ScheduleCron, out var schedule);
        return new BackupSettingsResponseDto
        {
            TenantId = row.TenantId,
            Enabled = row.Enabled,
            ScheduleCron = row.ScheduleCron,
            Schedule = schedule,
            RetentionDays = row.RetentionDays,
            LastRunAtUtc = row.LastRunAt,
            NextRunAtUtc = row.NextRunAt,
            UpdatedAtUtc = row.UpdatedAt ?? row.CreatedAt,
        };
    }
}
