using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.DataDeletion;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Publishes TSE failover activity events and emails Super Admins
/// (same channel pattern as <c>DataAccessNotificationService</c> / tenant-settings alerts).
/// </summary>
public sealed class TseFailoverNotificationService : ITseFailoverNotificationService
{
    private readonly IActivityEventPublisher _activity;
    private readonly IDataDeletionNotificationSender _email;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly ILogger<TseFailoverNotificationService> _logger;

    public TseFailoverNotificationService(
        IActivityEventPublisher activity,
        IDataDeletionNotificationSender email,
        UserManager<ApplicationUser> userManager,
        IOptionsMonitor<TseOptions> tseOptions,
        ILogger<TseFailoverNotificationService> logger)
    {
        _activity = activity;
        _email = email;
        _userManager = userManager;
        _tseOptions = tseOptions;
        _logger = logger;
    }

    public Task NotifyFailoverStartedAsync(
        TseDevice primary,
        TseDevice backup,
        CancellationToken cancellationToken = default)
    {
        var labelPrimary = DeviceLabel(primary);
        var labelBackup = DeviceLabel(backup);
        var message =
            $"TSE failover started: {labelPrimary} → {labelBackup}. Reason: {primary.HealthMessage ?? "n/a"}";

        return NotifyAsync(
            primary,
            ActivityEventType.TseFailoverStarted,
            subject: $"[URGENT] TSE Failover Started — Tenant {primary.TenantId}",
            emailBody: BuildBody(
                "TSE Failover has been triggered.",
                primary,
                backup,
                extraLines:
                [
                    $"Reason: {primary.HealthMessage ?? "n/a"}",
                    $"Started at (UTC): {DateTime.UtcNow:u}",
                    string.Empty,
                    "Please monitor the cash register / signing path.",
                ]),
            metadata: new
            {
                PrimaryDeviceId = primary.Id.ToString("D"),
                BackupDeviceId = backup.Id.ToString("D"),
                PrimaryLabel = labelPrimary,
                BackupLabel = labelBackup,
                HealthStatus = primary.HealthStatus.ToString(),
                HealthMessage = primary.HealthMessage,
                CashRegisterId = primary.CashRegisterId ?? primary.KassenId,
                Message = message,
            },
            dedupKey: $"tse-failover-started:{primary.Id:N}:{backup.Id:N}:{DateTime.UtcNow:yyyyMMddHHmm}",
            sendEmail: true,
            cancellationToken);
    }

    public Task NotifyFailoverCompletedAsync(
        TseDevice primary,
        TseDevice backup,
        string failoverType,
        CancellationToken cancellationToken = default)
    {
        var labelPrimary = DeviceLabel(primary);
        var labelBackup = DeviceLabel(backup);
        var message = $"TSE {labelPrimary} failed over to {labelBackup} ({failoverType}).";

        return NotifyAsync(
            primary,
            ActivityEventType.TseFailoverActivated,
            subject: $"[URGENT] TSE Failover Completed — Tenant {primary.TenantId}",
            emailBody: BuildBody(
                "TSE Failover completed successfully. The backup device is now active for signing.",
                primary,
                backup,
                extraLines:
                [
                    $"Failover type: {failoverType}",
                    $"Completed at (UTC): {DateTime.UtcNow:u}",
                ]),
            metadata: new
            {
                PrimaryDeviceId = primary.Id.ToString("D"),
                BackupDeviceId = backup.Id.ToString("D"),
                PrimaryLabel = labelPrimary,
                BackupLabel = labelBackup,
                FailoverType = failoverType,
                Message = message,
            },
            dedupKey: $"tse-failover-completed:{primary.Id:N}:{backup.Id:N}:{DateTime.UtcNow:yyyyMMddHHmmss}",
            sendEmail: true,
            cancellationToken);
    }

    public Task NotifyFailoverFailedAsync(
        TseDevice primary,
        TseDevice? backup,
        Exception? ex = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var detail = errorMessage
                     ?? ex?.Message
                     ?? "Unknown failover failure";
        var message = $"TSE failover failed for {DeviceLabel(primary)}: {detail}";

        return NotifyAsync(
            primary,
            ActivityEventType.TseFailoverFailed,
            subject: $"[CRITICAL] TSE Failover Failed — Tenant {primary.TenantId}",
            emailBody: BuildBody(
                "CRITICAL: TSE failover attempt failed.",
                primary,
                backup,
                extraLines:
                [
                    $"Error: {detail}",
                    $"Failed at (UTC): {DateTime.UtcNow:u}",
                    string.Empty,
                    "IMMEDIATE ACTION MAY BE REQUIRED — payments can be blocked if no signing path remains.",
                ]),
            metadata: new
            {
                PrimaryDeviceId = primary.Id.ToString("D"),
                BackupDeviceId = backup?.Id.ToString("D"),
                ErrorMessage = detail,
                Message = message,
            },
            dedupKey: $"tse-failover-failed:{primary.Id:N}:{DateTime.UtcNow:yyyyMMddHH}",
            sendEmail: true,
            cancellationToken);
    }

    public Task NotifyNoBackupAvailableAsync(
        TseDevice primary,
        string? healthMessage = null,
        CancellationToken cancellationToken = default)
    {
        var reason = healthMessage ?? primary.HealthMessage ?? "n/a";
        var message =
            $"CRITICAL: primary TSE {DeviceLabel(primary)} is unhealthy and no healthy backup is available.";

        return NotifyAsync(
            primary,
            ActivityEventType.TseFailoverNoBackup,
            subject: $"[CRITICAL] No TSE Backup Available — {primary.TenantId}",
            emailBody: BuildBody(
                "CRITICAL: TSE primary device is unhealthy and NO healthy backup is available!",
                primary,
                backup: null,
                extraLines:
                [
                    $"Status: {primary.HealthStatus}",
                    $"Message: {reason}",
                    string.Empty,
                    "IMMEDIATE ACTION REQUIRED!",
                    "The cash register may be unable to process fiscal payments.",
                ]),
            metadata: new
            {
                PrimaryDeviceId = primary.Id.ToString("D"),
                HealthStatus = primary.HealthStatus.ToString(),
                HealthMessage = reason,
                Message = message,
            },
            dedupKey: $"tse-failover-no-backup:{primary.Id:N}:{DateTime.UtcNow:yyyyMMddHH}",
            sendEmail: true,
            cancellationToken);
    }

    public Task NotifyBackupLowHealthAsync(
        TseDevice backup,
        CancellationToken cancellationToken = default)
    {
        var tenantId = backup.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            return Task.CompletedTask;

        var label = DeviceLabel(backup);
        var message =
            $"Backup TSE {label} health is low (score={backup.HealthScore}, status={backup.HealthStatus}).";

        return NotifyAsync(
            tenantId,
            ActivityEventType.TseFailoverBackupLowHealth,
            subject: $"[WARNING] TSE Backup Low Health — Tenant {tenantId}",
            emailBody: $@"
TSE backup device health is degraded.

Backup TSE: {label}
Device Id: {backup.Id}
Primary Id: {backup.PrimaryDeviceId}
Tenant ID: {tenantId}
Health status: {backup.HealthStatus}
Health score: {backup.HealthScore}
Health message: {backup.HealthMessage ?? "n/a"}
Checked at (UTC): {DateTime.UtcNow:u}

Consider provisioning or repairing a healthy backup before the primary fails.
".Trim(),
            metadata: new
            {
                BackupDeviceId = backup.Id.ToString("D"),
                PrimaryDeviceId = backup.PrimaryDeviceId?.ToString("D"),
                HealthStatus = backup.HealthStatus.ToString(),
                HealthScore = backup.HealthScore,
                HealthMessage = backup.HealthMessage,
                Message = message,
            },
            actorUserId: TseFailoverService.SystemActorUserId,
            dedupKey: $"tse-backup-low-health:{backup.Id:N}:{DateTime.UtcNow:yyyyMMddHH}",
            sendEmail: true,
            cancellationToken);
    }

    public Task NotifyFailoverRevertedAsync(
        TseDevice primary,
        CancellationToken cancellationToken = default)
    {
        var label = DeviceLabel(primary);
        var message = $"TSE signing reverted to primary {label}.";

        return NotifyAsync(
            primary,
            ActivityEventType.TseFailoverReverted,
            subject: $"[INFO] TSE Failover Reverted — Tenant {primary.TenantId}",
            emailBody: BuildBody(
                "TSE failover was reverted; the primary device is signing again.",
                primary,
                backup: null,
                extraLines:
                [
                    $"Reverted at (UTC): {DateTime.UtcNow:u}",
                ]),
            metadata: new
            {
                PrimaryDeviceId = primary.Id.ToString("D"),
                PrimaryLabel = label,
                Message = message,
            },
            dedupKey: $"tse-failover-revert:{primary.Id:N}:{DateTime.UtcNow:yyyyMMddHHmm}",
            sendEmail: false,
            cancellationToken);
    }

    private Task NotifyAsync(
        TseDevice primary,
        ActivityEventType type,
        string subject,
        string emailBody,
        object metadata,
        string dedupKey,
        bool sendEmail,
        CancellationToken cancellationToken)
    {
        var tenantId = primary.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            _logger.LogWarning(
                "TSE failover notify skipped (no tenant). Type={Type}, PrimaryId={PrimaryId}",
                type,
                primary.Id);
            return Task.CompletedTask;
        }

        return NotifyAsync(
            tenantId,
            type,
            subject,
            emailBody,
            metadata,
            actorUserId: TseFailoverService.SystemActorUserId,
            dedupKey,
            sendEmail,
            cancellationToken);
    }

    private async Task NotifyAsync(
        Guid tenantId,
        ActivityEventType type,
        string subject,
        string emailBody,
        object metadata,
        string actorUserId,
        string dedupKey,
        bool sendEmail,
        CancellationToken cancellationToken)
    {
        try
        {
            await _activity.TryPublishAsync(
                    tenantId,
                    type,
                    metadata,
                    actorUserId: actorUserId,
                    dedupKey: dedupKey,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "TSE failover activity publish failed. Type={Type}, TenantId={TenantId}",
                type,
                tenantId);
        }

        if (!sendEmail)
            return;

        try
        {
            var recipients = await ResolveRecipientsAsync().ConfigureAwait(false);
            if (recipients.Count == 0)
            {
                _logger.LogInformation(
                    "TSE failover Super Admin notify: no email recipients. Type={Type}, TenantId={TenantId}",
                    type,
                    tenantId);
                return;
            }

            await _email.SendAsync(
                    to: recipients,
                    cc: Array.Empty<string>(),
                    subject: subject,
                    plainBody: emailBody,
                    ct: cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "TSE failover Super Admin email sent. Type={Type}, TenantId={TenantId}, Recipients={Count}",
                type,
                tenantId,
                recipients.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "TSE failover Super Admin email failed. Type={Type}, TenantId={TenantId}",
                type,
                tenantId);
        }
    }

    private async Task<IReadOnlyList<string>> ResolveRecipientsAsync()
    {
        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var superAdmins = await _userManager.GetUsersInRoleAsync(Roles.SuperAdmin).ConfigureAwait(false);
        foreach (var user in superAdmins)
        {
            if (user.EmailConfirmed && !string.IsNullOrWhiteSpace(user.Email))
                emails.Add(user.Email.Trim());
        }

        var configured = _tseOptions.CurrentValue.FailoverAlertEmails;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            foreach (var part in configured.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.Contains('@', StringComparison.Ordinal))
                    emails.Add(part);
            }
        }

        return emails.ToList();
    }

    private static string DeviceLabel(TseDevice device) =>
        !string.IsNullOrWhiteSpace(device.DeviceId)
            ? device.DeviceId!
            : device.SerialNumber;

    private static string BuildBody(
        string intro,
        TseDevice primary,
        TseDevice? backup,
        IEnumerable<string>? extraLines = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine(intro);
        sb.AppendLine();
        sb.AppendLine($"Primary TSE: {DeviceLabel(primary)}");
        sb.AppendLine($"Primary Id: {primary.Id}");
        if (backup is not null)
        {
            sb.AppendLine($"Backup TSE: {DeviceLabel(backup)}");
            sb.AppendLine($"Backup Id: {backup.Id}");
        }

        sb.AppendLine($"Tenant ID: {primary.TenantId}");
        sb.AppendLine($"Cash Register: {primary.CashRegisterId ?? primary.KassenId}");
        if (extraLines is not null)
        {
            sb.AppendLine();
            foreach (var line in extraLines)
                sb.AppendLine(line);
        }

        return sb.ToString().TrimEnd();
    }
}
