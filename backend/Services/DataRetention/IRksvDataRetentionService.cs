namespace KasseAPI_Final.Services.DataRetention;

public interface IRksvDataRetentionService
{
    /// <summary>
    /// Returns RKSV vs non-RKSV storage/retention status for a tenant.
    /// Cross-tenant callers must authorize before invoking; this service does not enforce RBAC.
    /// </summary>
    Task<RetentionReport> GetRetentionStatusAsync(Guid tenantId, CancellationToken ct = default);
}
