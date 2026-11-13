using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

public sealed class BackupRetentionPolicyService : IBackupRetentionPolicyService
{
    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly ICloudStorageService _cloud;
    private readonly IBackupStorageCostService _costs;
    private readonly IAuditLogService _audit;
    private readonly ILogger<BackupRetentionPolicyService> _logger;

    public BackupRetentionPolicyService(
        AppDbContext db,
        IOptionsMonitor<BackupOptions> options,
        ICloudStorageService cloud,
        IBackupStorageCostService costs,
        IAuditLogService audit,
        ILogger<BackupRetentionPolicyService> logger)
    {
        _db = db;
        _options = options;
        _cloud = cloud;
        _costs = costs;
        _audit = audit;
        _logger = logger;
    }

    public async Task<BackupRetentionPolicySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var row = await EnsureTrackedAsync(cancellationToken).ConfigureAwait(false);
        return MapSnapshot(row);
    }

    public async Task<BackupRetentionPolicyResponseDto> GetAsync(
        bool includeCosts,
        BackupRunAccessScope? accessScope,
        CancellationToken cancellationToken = default)
    {
        var snap = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        BackupStorageCostResponseDto? costs = null;
        if (includeCosts)
            costs = await _costs.GetAsync(accessScope, cancellationToken).ConfigureAwait(false);
        return MapDto(snap, costs);
    }

    public async Task<BackupRetentionPolicyResponseDto> UpdateAsync(
        BackupRetentionPolicyPutRequestDto dto,
        string? actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var windows = BackupRetentionWindows.FromPolicy(dto.HotRetentionDays, dto.WarmRetentionDays);
        if (dto.HotRetentionDays < BackupRetentionWindows.MinHotDays
            || dto.HotRetentionDays > BackupRetentionWindows.MaxHotDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dto.HotRetentionDays),
                dto.HotRetentionDays,
                $"HotRetentionDays must be between {BackupRetentionWindows.MinHotDays} and {BackupRetentionWindows.MaxHotDays}.");
        }

        if (dto.WarmRetentionDays < BackupRetentionWindows.MinWarmDays
            || dto.WarmRetentionDays > BackupRetentionWindows.MaxWarmDays
            || dto.WarmRetentionDays < dto.HotRetentionDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dto.WarmRetentionDays),
                dto.WarmRetentionDays,
                $"WarmRetentionDays must be between {Math.Max(BackupRetentionWindows.MinWarmDays, dto.HotRetentionDays)} and {BackupRetentionWindows.MaxWarmDays}.");
        }

        if (dto.ColdRetentionYears < BackupRetentionPolicySettings.MinColdRetentionYears
            || dto.ColdRetentionYears > BackupRetentionPolicySettings.MaxColdRetentionYears)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dto.ColdRetentionYears),
                dto.ColdRetentionYears,
                $"ColdRetentionYears must be between {BackupRetentionPolicySettings.MinColdRetentionYears} and {BackupRetentionPolicySettings.MaxColdRetentionYears} (RKSV / BAO §132).");
        }

        if (!dto.LegalRetentionEnforced)
        {
            throw new ArgumentException(
                "LegalRetentionEnforced cannot be disabled — System backups must retain 7 years (RKSV / BAO §132).",
                nameof(dto.LegalRetentionEnforced));
        }

        var row = await EnsureTrackedAsync(cancellationToken).ConfigureAwait(false);
        var oldValues = new
        {
            row.HotRetentionDays,
            row.WarmRetentionDays,
            row.ColdRetentionYears,
            row.ColdStorageEnabled,
            row.LegalRetentionEnforced
        };

        row.HotRetentionDays = windows.HotDays;
        row.WarmRetentionDays = windows.WarmDays;
        row.ColdRetentionYears = dto.ColdRetentionYears;
        row.ColdStorageEnabled = dto.ColdStorageEnabled;
        row.LegalRetentionEnforced = true;
        row.CloudProvider = CloudStorageService.ParseProvider(_options.CurrentValue.CloudStorage.Provider).ToString();
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.UpdatedByUserId = actorUserId;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var newValues = new
        {
            row.HotRetentionDays,
            row.WarmRetentionDays,
            row.ColdRetentionYears,
            row.ColdStorageEnabled,
            row.LegalRetentionEnforced
        };

        await _audit.LogSystemOperationAsync(
            action: "BACKUP_RETENTION_POLICY_UPDATED",
            entityType: "BackupRetentionPolicy",
            userId: actorUserId ?? "unknown",
            userRole: actorRole,
            description: "Backup retention policy updated.",
            status: AuditLogStatus.Success,
            actionType: AuditEventType.BackupRetentionPolicyUpdated,
            oldValues: oldValues,
            newValues: newValues).ConfigureAwait(false);

        _logger.LogInformation(
            "Backup retention policy updated: hot={Hot}d warm={Warm}d coldYears={Years} coldEnabled={Cold} actor={Actor}",
            row.HotRetentionDays,
            row.WarmRetentionDays,
            row.ColdRetentionYears,
            row.ColdStorageEnabled,
            actorUserId);

        var snap = MapSnapshot(row);
        var costs = await _costs.GetAsync(null, cancellationToken).ConfigureAwait(false);
        return MapDto(snap, costs);
    }

    public async Task ApplyDefaultLegalHoldIfRequiredAsync(
        BackupRun run,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.Strategy != BackupStrategyKind.System || run.Status != BackupRunStatus.Succeeded)
            return;

        var snap = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (!snap.LegalRetentionEnforced)
            return;

        var years = Math.Max(snap.ColdRetentionYears, BackupRetentionPolicySettings.MinColdRetentionYears);
        var until = (run.CompletedAt ?? run.RequestedAt).AddYears(years);
        if (run.LegalHold && run.LegalHoldUntilUtc is DateTime existing && existing >= until)
            return;

        run.LegalHold = true;
        run.LegalHoldUntilUtc = until;
        run.LegalHoldReason = "RKSV / BAO §132 legal retention (System backup)";
        run.LegalHoldSetByUserId = actorUserId ?? "system";
        run.LegalHoldSetAtUtc = DateTime.UtcNow;
        _logger.LogInformation(
            "Applied System legal hold: runId={RunId} until={Until:o}",
            run.Id,
            until);
    }

    private async Task<BackupRetentionPolicySettings> EnsureTrackedAsync(CancellationToken cancellationToken)
    {
        var row = await _db.BackupRetentionPolicySettings
            .FirstOrDefaultAsync(x => x.Id == BackupRetentionPolicySettings.SingletonId, cancellationToken)
            .ConfigureAwait(false);
        if (row != null)
            return row;

        row = new BackupRetentionPolicySettings
        {
            Id = BackupRetentionPolicySettings.SingletonId,
            HotRetentionDays = BackupRetentionWindows.DefaultHotDays,
            WarmRetentionDays = BackupRetentionWindows.DefaultWarmDays,
            ColdRetentionYears = BackupRetentionPolicySettings.DefaultColdRetentionYears,
            ColdStorageEnabled = false,
            LegalRetentionEnforced = true,
            CloudProvider = CloudStorageService.ParseProvider(_options.CurrentValue.CloudStorage.Provider).ToString(),
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.BackupRetentionPolicySettings.Add(row);
        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            row = await _db.BackupRetentionPolicySettings
                .FirstAsync(x => x.Id == BackupRetentionPolicySettings.SingletonId, cancellationToken)
                .ConfigureAwait(false);
        }

        return row;
    }

    private BackupRetentionPolicySnapshot MapSnapshot(BackupRetentionPolicySettings row)
    {
        var cloudOpts = _options.CurrentValue.CloudStorage;
        var configuredProvider = CloudStorageService.ParseProvider(
            string.IsNullOrWhiteSpace(row.CloudProvider) ? cloudOpts.Provider : row.CloudProvider);
        return new BackupRetentionPolicySnapshot
        {
            HotRetentionDays = row.HotRetentionDays,
            WarmRetentionDays = row.WarmRetentionDays,
            ColdRetentionYears = row.ColdRetentionYears,
            ColdStorageEnabled = row.ColdStorageEnabled,
            LegalRetentionEnforced = row.LegalRetentionEnforced,
            CloudProvider = configuredProvider,
            CloudConfigured = _cloud.IsConfigured,
            CloudFallbackToFilesystem = cloudOpts.FallbackToFilesystemWhenUnconfigured,
            UpdatedAtUtc = row.UpdatedAtUtc,
            UpdatedByUserId = row.UpdatedByUserId
        };
    }

    private static BackupRetentionPolicyResponseDto MapDto(
        BackupRetentionPolicySnapshot snap,
        BackupStorageCostResponseDto? costs) =>
        new()
        {
            HotRetentionDays = snap.HotRetentionDays,
            WarmRetentionDays = snap.WarmRetentionDays,
            ColdRetentionYears = snap.ColdRetentionYears,
            ColdStorageEnabled = snap.ColdStorageEnabled,
            LegalRetentionEnforced = snap.LegalRetentionEnforced,
            CloudProvider = snap.CloudProvider.ToString(),
            CloudConfigured = snap.CloudConfigured,
            CloudFallbackToFilesystem = snap.CloudFallbackToFilesystem,
            LegalBasis = "RKSV / BAO §132 (7-year retention)",
            SystemLegalRetentionDays = snap.ColdRetentionYears * 365,
            UpdatedAtUtc = snap.UpdatedAtUtc,
            UpdatedByUserId = snap.UpdatedByUserId,
            StorageCosts = costs
        };
}
