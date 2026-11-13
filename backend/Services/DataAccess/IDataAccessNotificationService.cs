namespace KasseAPI_Final.Services.DataAccess;

public interface IDataAccessNotificationService
{
    /// <summary>Notifies Super Admins about a data-access event (activity feed + best-effort email).</summary>
    Task NotifySuperAdminAsync(
        Guid tenantId,
        Guid requestId,
        string subject,
        string body,
        CancellationToken ct = default);

    /// <summary>Notifies the requesting user that an export download link is ready.</summary>
    Task NotifyUserAsync(
        string? userId,
        Guid tenantId,
        Guid requestId,
        string subject,
        string body,
        CancellationToken ct = default);

    /// <summary>
    /// Export-ready notify with HTML + plain bodies (activity feed uses plain text).
    /// </summary>
    Task NotifyUserAsync(
        string? userId,
        Guid tenantId,
        Guid requestId,
        string subject,
        string plainBody,
        string? htmlBody,
        CancellationToken ct = default);
}
