using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Billing;

public interface IBillingAuditService
{
    /// <summary>Log a billing action (actor resolved from HTTP context when available).</summary>
    Task LogAsync(
        string action,
        Guid? tenantId,
        Guid? saleId,
        string? details = null,
        string? ipAddress = null,
        CancellationToken ct = default);

    /// <summary>Log a billing action with an explicit actor user id.</summary>
    Task LogAsync(
        string action,
        Guid actorUserId,
        Guid? tenantId,
        Guid? saleId,
        string? details = null,
        string? ipAddress = null,
        CancellationToken ct = default);

    Task<BillingAuditLogListResponse> ListAsync(
        BillingAuditLogQuery query,
        CancellationToken ct = default);

    Task<List<BillingAuditLogResponse>> GetForSaleAsync(
        Guid saleId,
        CancellationToken ct = default);

    Task<List<BillingAuditLogResponse>> GetForTenantAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task LogLicenseSoldAsync(
        LicenseSale sale,
        Guid actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task LogLicenseCancelledAsync(
        LicenseSale sale,
        Guid actorUserId,
        string cancellationReason,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task LogLicenseActivatedAsync(
        LicenseSale sale,
        Guid actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task LogLicenseExtendedAsync(
        LicenseSale sale,
        Guid actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);
}
