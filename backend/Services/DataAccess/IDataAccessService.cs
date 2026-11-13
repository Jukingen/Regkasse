namespace KasseAPI_Final.Services.DataAccess;

public interface IDataAccessService
{
    /// <summary>
    /// Access-control entrypoint: auto-approves View/Export and processes immediately;
    /// Delete requires manual approval (Super Admin notify + <c>pending_approval</c>).
    /// </summary>
    Task<DataAccessResult> ProcessRequestAsync(
        Guid tenantId,
        DataRequestType type,
        Guid userId,
        string? reason = null,
        CancellationToken ct = default);
}
