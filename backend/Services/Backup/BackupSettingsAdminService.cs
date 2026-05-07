using Cronos;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Admin-facing backup automation settings (cron + retention) backed by backup_settings singleton.</summary>
public sealed class BackupSettingsAdminService : IBackupSettingsAdminService
{
    public const int MinRetentionDays = 1;
    public const int MaxRetentionDays = 3650;

    private readonly AppDbContext _db;
    private readonly ILogger<BackupSettingsAdminService> _logger;

    public BackupSettingsAdminService(AppDbContext db, ILogger<BackupSettingsAdminService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<BackupSettingsResponseDto> GetAsync(CancellationToken cancellationToken = default)
    {
        await BackupSettingsEnsure.EnsureSingletonAsync(_db, cancellationToken);
        var row = await _db.BackupSettings.AsNoTracking()
            .FirstAsync(x => x.Id == BackupSettings.SingletonId, cancellationToken);
        return Map(row);
    }

    public async Task<BackupSettingsResponseDto> PutAsync(
        BackupSettingsPutRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        var cron = (dto.ScheduleCron ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(cron) || !CronExpression.TryParse(cron, CronFormat.Standard, out _))
            throw new ArgumentException("INVALID_CRON_EXPRESSION", nameof(dto));

        if (dto.RetentionDays < MinRetentionDays || dto.RetentionDays > MaxRetentionDays)
            throw new ArgumentOutOfRangeException(nameof(dto), dto.RetentionDays,
                $"RetentionDays must be between {MinRetentionDays} and {MaxRetentionDays}.");

        await BackupSettingsEnsure.EnsureSingletonAsync(_db, cancellationToken);
        var row = await _db.BackupSettings.FirstAsync(x => x.Id == BackupSettings.SingletonId, cancellationToken);

        row.Enabled = dto.Enabled;
        row.ScheduleCron = cron;
        row.RetentionDays = dto.RetentionDays;
        row.UpdatedAtUtc = DateTime.UtcNow;

        var utcNow = DateTime.UtcNow;
        RefreshNextProjection(row, utcNow);

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Backup settings updated: Enabled={Enabled}, RetentionDays={RetentionDays}",
            row.Enabled,
            row.RetentionDays);
        return Map(row);
    }

    public async Task<BackupScheduleStatusResponseDto> GetScheduleStatusAsync(CancellationToken cancellationToken = default)
    {
        await BackupSettingsEnsure.EnsureSingletonAsync(_db, cancellationToken);
        var row = await _db.BackupSettings.AsNoTracking()
            .FirstAsync(x => x.Id == BackupSettings.SingletonId, cancellationToken);

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

    private static void RefreshNextProjection(BackupSettings row, DateTime utcNow)
    {
        if (!row.Enabled
            || string.IsNullOrWhiteSpace(row.ScheduleCron)
            || !CronExpression.TryParse(row.ScheduleCron.Trim(), CronFormat.Standard, out var expr))
        {
            row.NextRunAt = null;
            return;
        }

        row.NextRunAt = expr.GetNextOccurrence(utcNow, TimeZoneInfo.Utc, inclusive: false);
    }

    private static BackupSettingsResponseDto Map(BackupSettings row) =>
        new()
        {
            Enabled = row.Enabled,
            ScheduleCron = row.ScheduleCron,
            RetentionDays = row.RetentionDays,
            LastRunAtUtc = row.LastRunAt,
            NextRunAtUtc = row.NextRunAt,
            UpdatedAtUtc = row.UpdatedAtUtc
        };
}
