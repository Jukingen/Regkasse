using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Automatic / manual TSE primary → backup failover using persisted device health and failover logs.
/// </summary>
public sealed class TseFailoverService : ITseFailoverService
{
    public const string SystemActorUserId = "system";

    private const string AuditEntityType = "TseDevice";

    private readonly AppDbContext _db;
    private readonly ITseDeviceHealthCheckService _healthCheck;
    private readonly IAuditLogService _auditLog;
    private readonly ITseFailoverNotificationService _notifications;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly ILogger<TseFailoverService> _logger;

    public TseFailoverService(
        AppDbContext db,
        ITseDeviceHealthCheckService healthCheck,
        IAuditLogService auditLog,
        ITseFailoverNotificationService notifications,
        UserManager<ApplicationUser> userManager,
        IOptionsMonitor<TseOptions> tseOptions,
        ILogger<TseFailoverService> logger)
    {
        _db = db;
        _healthCheck = healthCheck;
        _auditLog = auditLog;
        _notifications = notifications;
        _userManager = userManager;
        _tseOptions = tseOptions;
        _logger = logger;
    }

    public async Task<FailoverResult> CheckAndFailoverAsync(
        Guid primaryDeviceId,
        CancellationToken cancellationToken = default)
    {
        if (!_tseOptions.CurrentValue.AutoFailoverEnabled)
            return FailoverResult.Fail("Automatic TSE failover is disabled.", primaryDeviceId);

        var primary = await LoadPrimaryWithBackupsAsync(primaryDeviceId, cancellationToken).ConfigureAwait(false);
        if (primary is null)
            return FailoverResult.Fail("Primary device not found", primaryDeviceId);

        var health = await _healthCheck.CheckHealthAsync(primaryDeviceId, cancellationToken).ConfigureAwait(false);

        // Refresh primary entity after health persistence
        await _db.Entry(primary).ReloadAsync(cancellationToken).ConfigureAwait(false);

        if (health.IsHealthy && health.Status == TseHealthStatus.Healthy)
        {
            if (HasActiveFailover(primary))
            {
                return await RevertToPrimaryAsync(primaryDeviceId, SystemActorUserId, cancellationToken)
                    .ConfigureAwait(false);
            }

            return FailoverResult.Success(
                "Primary is healthy, no failover needed",
                primary.Id,
                failoverType: TseFailoverTypes.Automatic);
        }

        // Degraded: do not auto-failover (warn only via health columns).
        if (health.Status == TseHealthStatus.Degraded)
        {
            return FailoverResult.Success(
                "Primary is degraded; automatic failover not triggered",
                primary.Id,
                failoverType: TseFailoverTypes.Automatic);
        }

        var trigger = health.Status switch
        {
            TseHealthStatus.Expired => TseFailoverTriggerReasons.Expired,
            TseHealthStatus.Revoked => TseFailoverTriggerReasons.Revoked,
            _ => TseFailoverTriggerReasons.HealthCheckFailed,
        };

        var backup = SelectHealthyBackup(primary);
        if (backup is null)
        {
            await NotifyNoHealthyBackupAsync(primary, health, cancellationToken).ConfigureAwait(false);
            return FailoverResult.Fail(
                "No healthy backup available",
                primary.Id,
                needsAttention: true);
        }

        return await PerformFailoverAsync(
                primary,
                backup,
                TseFailoverTypes.Automatic,
                trigger,
                SystemActorUserId,
                notes: health.Message,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<FailoverResult> ManualFailoverAsync(
        Guid primaryDeviceId,
        Guid backupDeviceId,
        string performedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(performedByUserId))
            return FailoverResult.Fail("performedByUserId is required", primaryDeviceId, backupDeviceId);

        if (!await IsSuperAdminAsync(performedByUserId).ConfigureAwait(false))
            return FailoverResult.Fail("Only SuperAdmin can perform manual failover", primaryDeviceId, backupDeviceId);

        var primary = await LoadPrimaryWithBackupsAsync(primaryDeviceId, cancellationToken).ConfigureAwait(false);
        if (primary is null)
            return FailoverResult.Fail("Primary device not found", primaryDeviceId, backupDeviceId);

        var backup = await _db.TseDevices
            .FirstOrDefaultAsync(d => d.Id == backupDeviceId, cancellationToken)
            .ConfigureAwait(false);

        if (backup is null)
            return FailoverResult.Fail("Backup device not found", primaryDeviceId, backupDeviceId);

        if (!backup.IsBackup || backup.PrimaryDeviceId != primary.Id)
        {
            // Allow explicit link if missing but same register/tenant
            if (backup.PrimaryDeviceId is null
                && primary.CashRegisterId.HasValue
                && backup.CashRegisterId == primary.CashRegisterId)
            {
                backup.IsBackup = true;
                backup.IsPrimary = false;
                backup.PrimaryDeviceId = primary.Id;
            }
            else if (backup.PrimaryDeviceId != primary.Id)
            {
                return FailoverResult.Fail(
                    "Backup device is not linked to the primary",
                    primaryDeviceId,
                    backupDeviceId,
                    failoverType: TseFailoverTypes.Manual);
            }
        }

        return await PerformFailoverAsync(
                primary,
                backup,
                TseFailoverTypes.Manual,
                TseFailoverTriggerReasons.ManualOverride,
                performedByUserId,
                notes: "Manual override by SuperAdmin",
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<FailoverResult> RevertToPrimaryAsync(
        Guid primaryDeviceId,
        string performedByUserId,
        CancellationToken cancellationToken = default)
    {
        var primary = await LoadPrimaryWithBackupsAsync(primaryDeviceId, cancellationToken).ConfigureAwait(false);
        if (primary is null)
            return FailoverResult.Fail("Primary device not found", primaryDeviceId);

        if (primary.TenantId is null || primary.TenantId == Guid.Empty)
            return FailoverResult.Fail("Primary device has no tenant id", primaryDeviceId);

        var activeBackups = primary.BackupDevices.Where(b => b.IsFailoverActive).ToList();
        if (activeBackups.Count == 0 && !primary.IsFailoverActive)
        {
            return FailoverResult.Success(
                "Primary is already active (no failover state)",
                primary.Id,
                failoverType: TseFailoverTypes.Automatic);
        }

        var log = new TseFailoverLog
        {
            Id = Guid.NewGuid(),
            TenantId = primary.TenantId.Value,
            PrimaryDeviceId = primary.Id,
            BackupDeviceId = activeBackups.FirstOrDefault()?.Id,
            FailoverType = string.Equals(performedByUserId, SystemActorUserId, StringComparison.Ordinal)
                ? TseFailoverTypes.Automatic
                : TseFailoverTypes.Manual,
            TriggerReason = TseFailoverTriggerReasons.ManualOverride,
            PreviousStatus = "FailoverActive",
            NewStatus = "PrimaryActive",
            StartedAt = DateTime.UtcNow,
            PerformedBy = performedByUserId,
            Notes = "Revert to primary",
        };

        try
        {
            foreach (var backup in activeBackups)
            {
                backup.IsFailoverActive = false;
                backup.UpdatedAt = DateTime.UtcNow;
            }

            primary.IsFailoverActive = false;
            primary.UpdatedAt = DateTime.UtcNow;

            await BindDefaultTseDeviceAsync(primary.TenantId.Value, primary.Id, cancellationToken)
                .ConfigureAwait(false);

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            log.IsSuccessful = true;
            log.CompletedAt = DateTime.UtcNow;
            _db.TseFailoverLogs.Add(log);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await TryAuditAsync(
                    "TSE_FAILOVER_REVERT",
                    primary.TenantId.Value,
                    primary.Id,
                    $"Reverted to primary TSE device {primary.Id}",
                    performedByUserId,
                    AuditLogStatus.Success,
                    new { primary.Id, BackupIds = activeBackups.Select(b => b.Id).ToArray() },
                    cancellationToken)
                .ConfigureAwait(false);

            await _notifications.NotifyFailoverRevertedAsync(primary, cancellationToken)
                .ConfigureAwait(false);

            return FailoverResult.Success(
                "Reverted to primary TSE device",
                primary.Id,
                activeBackups.FirstOrDefault()?.Id,
                log.Id,
                log.FailoverType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Revert to primary failed for {PrimaryId}", primary.Id);
            log.IsSuccessful = false;
            log.ErrorMessage = Truncate(ex.Message, 2000);
            log.CompletedAt = DateTime.UtcNow;
            try
            {
                _db.TseFailoverLogs.Add(log);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception saveEx)
            {
                _logger.LogWarning(saveEx, "Failed to persist failover revert log for {PrimaryId}", primary.Id);
            }

            return FailoverResult.Fail($"Revert failed: {ex.Message}", primary.Id, logId: log.Id);
        }
    }

    public async Task<TseDevice?> GetActiveDeviceForRegisterAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        if (cashRegisterId == Guid.Empty)
            return null;

        var devices = await _db.TseDevices
            .Where(d => d.IsActive && (d.CashRegisterId == cashRegisterId || d.KassenId == cashRegisterId))
            .OrderByDescending(d => d.IsFailoverActive)
            .ThenByDescending(d => d.IsPrimary)
            .ThenByDescending(d => d.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var failover = devices.FirstOrDefault(d => d.IsFailoverActive && d.IsBackup);
        if (failover is not null)
            return failover;

        return devices.FirstOrDefault(d => d.IsPrimary)
               ?? devices.FirstOrDefault();
    }

    private async Task<FailoverResult> PerformFailoverAsync(
        TseDevice primary,
        TseDevice backup,
        string failoverType,
        string triggerReason,
        string performedByUserId,
        string? notes,
        CancellationToken cancellationToken)
    {
        if (primary.TenantId is null || primary.TenantId == Guid.Empty)
            return FailoverResult.Fail("Primary device has no tenant id", primary.Id, backup.Id, failoverType: failoverType);

        var log = new TseFailoverLog
        {
            Id = Guid.NewGuid(),
            TenantId = primary.TenantId.Value,
            PrimaryDeviceId = primary.Id,
            BackupDeviceId = backup.Id,
            FailoverType = failoverType,
            TriggerReason = triggerReason,
            PreviousStatus = primary.HealthStatus.ToString(),
            NewStatus = "FailoverActive",
            StartedAt = DateTime.UtcNow,
            PerformedBy = performedByUserId,
            Notes = Truncate(notes, 1000),
        };

        try
        {
            var backupHealth = await _healthCheck.CheckHealthAsync(backup.Id, cancellationToken)
                .ConfigureAwait(false);
            await _db.Entry(backup).ReloadAsync(cancellationToken).ConfigureAwait(false);

            if (!backupHealth.IsHealthy || backupHealth.Status is TseHealthStatus.Unhealthy or TseHealthStatus.Offline
                or TseHealthStatus.Expired or TseHealthStatus.Revoked)
            {
                log.IsSuccessful = false;
                log.ErrorMessage = "Backup device is not healthy";
                log.CompletedAt = DateTime.UtcNow;
                _db.TseFailoverLogs.Add(log);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await _notifications.NotifyBackupLowHealthAsync(backup, cancellationToken)
                    .ConfigureAwait(false);
                await _notifications.NotifyFailoverFailedAsync(
                        primary,
                        backup,
                        errorMessage: "Backup device is not healthy",
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                return FailoverResult.Fail(
                    "Backup device is not healthy",
                    primary.Id,
                    backup.Id,
                    log.Id,
                    failoverType,
                    needsAttention: true);
            }

            await _notifications.NotifyFailoverStartedAsync(primary, backup, cancellationToken)
                .ConfigureAwait(false);

            // Only the backup carries IsFailoverActive (signing role).
            foreach (var other in primary.BackupDevices.Where(b => b.Id != backup.Id && b.IsFailoverActive))
            {
                other.IsFailoverActive = false;
                other.UpdatedAt = DateTime.UtcNow;
            }

            backup.IsFailoverActive = true;
            backup.IsBackup = true;
            backup.IsPrimary = false;
            backup.PrimaryDeviceId ??= primary.Id;
            backup.UpdatedAt = DateTime.UtcNow;

            primary.IsFailoverActive = false;
            primary.LastFailoverAt = DateTime.UtcNow;
            primary.LastFailoverReason = Truncate(
                $"{triggerReason}: {primary.HealthMessage ?? notes}",
                500);
            primary.FailoverCount++;
            primary.UpdatedAt = DateTime.UtcNow;

            await BindDefaultTseDeviceAsync(primary.TenantId.Value, backup.Id, cancellationToken)
                .ConfigureAwait(false);

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            log.IsSuccessful = true;
            log.CompletedAt = DateTime.UtcNow;
            _db.TseFailoverLogs.Add(log);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await TryAuditAsync(
                    failoverType == TseFailoverTypes.Manual ? "TSE_MANUAL_FAILOVER" : "TSE_FAILOVER",
                    primary.TenantId.Value,
                    primary.Id,
                    $"Failover to backup {backup.Id} ({failoverType})",
                    performedByUserId,
                    AuditLogStatus.Success,
                    new
                    {
                        PrimaryDeviceId = primary.Id,
                        BackupDeviceId = backup.Id,
                        TriggerReason = triggerReason,
                        Reason = primary.HealthMessage,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            await _notifications.NotifyFailoverCompletedAsync(primary, backup, failoverType, cancellationToken)
                .ConfigureAwait(false);

            var label = string.IsNullOrWhiteSpace(backup.DeviceId) ? backup.SerialNumber : backup.DeviceId;
            return FailoverResult.Success(
                $"Failover to backup {label} successful",
                primary.Id,
                backup.Id,
                log.Id,
                failoverType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failover failed for primary {PrimaryId}", primary.Id);
            log.IsSuccessful = false;
            log.ErrorMessage = Truncate(ex.Message, 2000);
            log.CompletedAt = DateTime.UtcNow;
            try
            {
                _db.TseFailoverLogs.Add(log);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception saveEx)
            {
                _logger.LogWarning(saveEx, "Failed to persist failover failure log for {PrimaryId}", primary.Id);
            }

            await _notifications.NotifyFailoverFailedAsync(primary, backup, ex, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return FailoverResult.Fail($"Failover failed: {ex.Message}", primary.Id, backup.Id, log.Id, failoverType);
        }
    }

    private async Task<TseDevice?> LoadPrimaryWithBackupsAsync(Guid primaryDeviceId, CancellationToken cancellationToken)
    {
        return await _db.TseDevices
            .Include(d => d.BackupDevices)
            .FirstOrDefaultAsync(d => d.Id == primaryDeviceId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool HasActiveFailover(TseDevice primary) =>
        primary.BackupDevices.Any(b => b.IsFailoverActive);

    private static TseDevice? SelectHealthyBackup(TseDevice primary) =>
        primary.BackupDevices
            .Where(d => d.IsActive && d.IsBackup && d.HealthStatus == TseHealthStatus.Healthy)
            .OrderByDescending(d => d.HealthScore)
            .ThenBy(d => d.FailoverCount)
            .FirstOrDefault();

    private async Task NotifyNoHealthyBackupAsync(
        TseDevice primary,
        TseHealthResult health,
        CancellationToken cancellationToken)
    {
        var lowHealthBackups = primary.BackupDevices
            .Where(d => d.IsActive && d.IsBackup && d.HealthStatus != TseHealthStatus.Healthy)
            .OrderByDescending(d => d.HealthScore)
            .ToList();

        foreach (var backup in lowHealthBackups.Take(3))
        {
            await _notifications.NotifyBackupLowHealthAsync(backup, cancellationToken)
                .ConfigureAwait(false);
        }

        await _notifications.NotifyNoBackupAvailableAsync(primary, health.Message, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task BindDefaultTseDeviceAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken)
    {
        try
        {
            var settings = await _db.CompanySettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
                .ConfigureAwait(false);

            if (settings is null)
                return;

            settings.DefaultTseDeviceId = deviceId.ToString("D");
            settings.UpdatedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to bind DefaultTseDeviceId for tenant {TenantId} device {DeviceId}",
                tenantId,
                deviceId);
        }
    }

    private async Task<bool> IsSuperAdminAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
            return false;

        if (await _userManager.IsInRoleAsync(user, Roles.SuperAdmin).ConfigureAwait(false))
            return true;

        return string.Equals(user.Role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase);
    }

    private async Task TryAuditAsync(
        string action,
        Guid tenantId,
        Guid deviceId,
        string description,
        string userId,
        AuditLogStatus status,
        object? responseData,
        CancellationToken cancellationToken)
    {
        try
        {
            var role = string.Equals(userId, SystemActorUserId, StringComparison.Ordinal)
                ? "System"
                : Roles.SuperAdmin;

            await _auditLog.LogSystemOperationAsync(
                action,
                AuditEntityType,
                userId: userId,
                userRole: role,
                description: description,
                status: status,
                responseData: responseData,
                entityId: deviceId,
                tenantId: tenantId,
                actionType: AuditEventType.Other).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TSE failover audit failed for action {Action} tenant {TenantId}", action, tenantId);
        }

        _ = cancellationToken;
    }

    private static string? Truncate(string? value, int maxLen)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        return value.Length <= maxLen ? value : value[..maxLen];
    }
}
