using System.Text;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.DataDeletion;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.TenantSettings;

/// <summary>
/// Publishes tenant-settings activity events and emails Super Admins / Mandanten-Admins.
/// </summary>
public sealed class TenantSettingsNotificationService : ITenantSettingsNotificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly AppDbContext _db;
    private readonly IActivityEventPublisher _activity;
    private readonly IDataDeletionNotificationSender _email;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<TenantSettingsNotificationService> _logger;

    public TenantSettingsNotificationService(
        AppDbContext db,
        IActivityEventPublisher activity,
        IDataDeletionNotificationSender email,
        UserManager<ApplicationUser> userManager,
        ILogger<TenantSettingsNotificationService> logger)
    {
        _db = db;
        _activity = activity;
        _email = email;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task NotifySettingsChangeAsync(
        Guid tenantId,
        Guid changeId,
        ActivityEventType eventType,
        string settingType,
        object? oldValue,
        object? newValue,
        string changedByUserId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var oldDisplay = FormatValue(oldValue);
        var newDisplay = FormatValue(newValue);
        var message = BuildMessage(eventType, settingType, oldDisplay, newDisplay);

        try
        {
            await _activity.TryPublishAsync(
                    tenantId,
                    eventType,
                    new
                    {
                        ChangeId = changeId,
                        SettingType = settingType,
                        OldValue = oldDisplay,
                        NewValue = newDisplay,
                        ChangedBy = changedByUserId,
                        Reason = reason,
                        Message = message,
                    },
                    actorUserId: changedByUserId,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Tenant settings activity publish failed. TenantId={TenantId}, ChangeId={ChangeId}, Event={Event}",
                tenantId,
                changeId,
                eventType);
        }

        try
        {
            switch (eventType)
            {
                case ActivityEventType.TenantSettingsChangeRequested:
                    await NotifySuperAdminsAsync(
                            tenantId,
                            changeId,
                            subject: $"Tenant settings change requested: {settingType}",
                            body: BuildEmailBody(
                                greeting: "a Super Admin",
                                intro: "A sensitive tenant setting change was requested and awaits four-eyes approval.",
                                settingType,
                                oldDisplay,
                                newDisplay,
                                changedByUserId,
                                reason),
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case ActivityEventType.TenantSettingsChangeApproved:
                case ActivityEventType.TenantSettingsChangeReverted:
                    await NotifyTenantManagersAsync(
                            tenantId,
                            changeId,
                            subject: eventType == ActivityEventType.TenantSettingsChangeApproved
                                ? $"Tenant settings updated: {settingType}"
                                : $"Tenant settings reverted: {settingType}",
                            body: BuildEmailBody(
                                greeting: "Mandanten-Admin",
                                intro: eventType == ActivityEventType.TenantSettingsChangeApproved
                                    ? "The following tenant setting has been changed after Super Admin approval."
                                    : "The following tenant setting change has been reverted.",
                                settingType,
                                oldDisplay,
                                newDisplay,
                                changedByUserId,
                                reason),
                            excludeUserId: changedByUserId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case ActivityEventType.TenantSettingsChangeRejected:
                    // Activity feed is enough for rejections; no Manager email storm.
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Tenant settings email notify failed. TenantId={TenantId}, ChangeId={ChangeId}, Event={Event}",
                tenantId,
                changeId,
                eventType);
        }
    }

    private async Task NotifySuperAdminsAsync(
        Guid tenantId,
        Guid changeId,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var superAdmins = await _userManager.GetUsersInRoleAsync(Roles.SuperAdmin).ConfigureAwait(false);
        var emails = superAdmins
            .Where(u => u.EmailConfirmed && !string.IsNullOrWhiteSpace(u.Email))
            .Select(u => u.Email!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (emails.Count == 0)
        {
            _logger.LogInformation(
                "Tenant settings Super Admin notify: no email recipients. TenantId={TenantId}, ChangeId={ChangeId}",
                tenantId,
                changeId);
            return;
        }

        await _email.SendAsync(
                to: emails,
                cc: Array.Empty<string>(),
                subject: subject,
                plainBody: body,
                ct: cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Tenant settings Super Admin notify sent. TenantId={TenantId}, ChangeId={ChangeId}, Recipients={Count}",
            tenantId,
            changeId,
            emails.Count);
    }

    private async Task NotifyTenantManagersAsync(
        Guid tenantId,
        Guid changeId,
        string subject,
        string body,
        string? excludeUserId,
        CancellationToken cancellationToken)
    {
        var query = _db.UserTenantMemberships.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .Join(
                _db.Users.AsNoTracking(),
                m => m.UserId,
                u => u.Id,
                (m, u) => new { u.Email, u.Id, u.Role })
            .Where(x => x.Role == Roles.Manager && x.Email != null && x.Email != "");

        if (!string.IsNullOrWhiteSpace(excludeUserId))
            query = query.Where(x => x.Id != excludeUserId);

        var emails = await query
            .Select(x => x.Email!)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (emails.Count == 0)
        {
            // Fallback: active owner membership email when no Manager role users exist.
            var ownerEmail = await _db.UserTenantMemberships.AsNoTracking()
                .Where(m => m.TenantId == tenantId && m.IsActive && m.IsOwner)
                .Join(
                    _db.Users.AsNoTracking(),
                    m => m.UserId,
                    u => u.Id,
                    (_, u) => u.Email)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(ownerEmail))
                emails.Add(ownerEmail!);
        }

        if (emails.Count == 0)
        {
            _logger.LogInformation(
                "Tenant settings Manager notify: no email recipients. TenantId={TenantId}, ChangeId={ChangeId}",
                tenantId,
                changeId);
            return;
        }

        await _email.SendAsync(
                to: emails,
                cc: Array.Empty<string>(),
                subject: subject,
                plainBody: body,
                ct: cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Tenant settings Manager notify sent. TenantId={TenantId}, ChangeId={ChangeId}, Recipients={Count}",
            tenantId,
            changeId,
            emails.Count);
    }

    private static string BuildMessage(
        ActivityEventType eventType,
        string settingType,
        string oldDisplay,
        string newDisplay) =>
        eventType switch
        {
            ActivityEventType.TenantSettingsChangeRequested =>
                $"Settings change requested: {settingType} ({oldDisplay} → {newDisplay})",
            ActivityEventType.TenantSettingsChangeApproved =>
                $"Settings change approved: {settingType} ({oldDisplay} → {newDisplay})",
            ActivityEventType.TenantSettingsChangeRejected =>
                $"Settings change rejected: {settingType}",
            ActivityEventType.TenantSettingsChangeReverted =>
                $"Settings change reverted: {settingType} ({oldDisplay} → {newDisplay})",
            _ => $"Tenant settings event: {settingType}",
        };

    private static string BuildEmailBody(
        string greeting,
        string intro,
        string settingType,
        string oldDisplay,
        string newDisplay,
        string changedByUserId,
        string? reason)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Dear {greeting},");
        sb.AppendLine();
        sb.AppendLine(intro);
        sb.AppendLine();
        sb.AppendLine($"{settingType}: {oldDisplay} → {newDisplay}");
        sb.AppendLine($"Actor user id: {changedByUserId}");
        if (!string.IsNullOrWhiteSpace(reason))
            sb.AppendLine($"Reason: {reason.Trim()}");
        sb.AppendLine();
        sb.AppendLine("If you did not expect this change, please contact support immediately.");
        sb.AppendLine();
        sb.AppendLine("Regards,");
        sb.AppendLine("Regkasse Team");
        return sb.ToString();
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
            return "(null)";

        if (value is string s)
            return string.IsNullOrWhiteSpace(s) ? "(empty)" : s;

        if (value is JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString() ?? "(empty)",
                JsonValueKind.Null => "(null)",
                _ => el.GetRawText(),
            };
        }

        try
        {
            return JsonSerializer.Serialize(value, JsonOptions);
        }
        catch
        {
            return value.ToString() ?? "(unprintable)";
        }
    }
}
