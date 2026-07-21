using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.DataDeletion;
using Microsoft.AspNetCore.Identity;

namespace KasseAPI_Final.Services.DataAccess;

/// <summary>Notify channels for GDPR data-access (Super Admin delete + user export-ready).</summary>
public sealed class DataAccessNotificationService : IDataAccessNotificationService
{
    private readonly IActivityEventPublisher _activity;
    private readonly IDataDeletionNotificationSender _email;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DataAccessNotificationService> _logger;

    public DataAccessNotificationService(
        IActivityEventPublisher activity,
        IDataDeletionNotificationSender email,
        UserManager<ApplicationUser> userManager,
        ILogger<DataAccessNotificationService> logger)
    {
        _activity = activity;
        _email = email;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task NotifySuperAdminAsync(
        Guid tenantId,
        Guid requestId,
        string subject,
        string body,
        CancellationToken ct = default)
    {
        await _activity.TryPublishAsync(
            tenantId,
            ActivityEventType.DataAccessDeleteRequested,
            metadata: new
            {
                RequestId = requestId.ToString("D"),
                Message = body,
                Subject = subject,
            },
            cancellationToken: ct).ConfigureAwait(false);

        var superAdmins = await _userManager.GetUsersInRoleAsync(Roles.SuperAdmin).ConfigureAwait(false);
        var emails = superAdmins
            .Where(u => u.EmailConfirmed && !string.IsNullOrWhiteSpace(u.Email))
            .Select(u => u.Email!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (emails.Count == 0)
        {
            _logger.LogInformation(
                "Data access Super Admin notify: no email recipients. TenantId={TenantId}, RequestId={RequestId}",
                tenantId,
                requestId);
            return;
        }

        await _email.SendAsync(
            to: emails,
            cc: Array.Empty<string>(),
            subject: subject,
            plainBody: body,
            ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Data access Super Admin notify sent. TenantId={TenantId}, RequestId={RequestId}, Recipients={Count}",
            tenantId,
            requestId,
            emails.Count);
    }

    public async Task NotifyUserAsync(
        string? userId,
        Guid tenantId,
        Guid requestId,
        string subject,
        string body,
        CancellationToken ct = default)
    {
        await _activity.TryPublishAsync(
            tenantId,
            ActivityEventType.DataExportReady,
            metadata: new
            {
                RequestId = requestId.ToString("D"),
                Message = body,
                Subject = subject,
            },
            actorUserId: userId,
            cancellationToken: ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogInformation(
                "Data export user notify skipped (no requester). TenantId={TenantId}, RequestId={RequestId}",
                tenantId,
                requestId);
            return;
        }

        var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user == null || string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogInformation(
                "Data export user notify skipped (no email). UserId={UserId}, RequestId={RequestId}",
                userId,
                requestId);
            return;
        }

        await _email.SendAsync(
            to: new[] { user.Email },
            cc: Array.Empty<string>(),
            subject: subject,
            plainBody: body,
            ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Data export ready notify sent. UserId={UserId}, RequestId={RequestId}",
            userId,
            requestId);
    }
}
