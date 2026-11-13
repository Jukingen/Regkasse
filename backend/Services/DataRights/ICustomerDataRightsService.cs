using KasseAPI_Final.Services.DataDeletion;

namespace KasseAPI_Final.Services.DataRights;

public interface ICustomerDataRightsService
{
    IReadOnlyList<DataRightsRequestTypeCatalogItemDto> GetRequestTypeCatalog();

    Task<TenantDataRightsRequestDto> CreateAsync(
        Guid tenantId,
        string requestType,
        string? reason,
        string? requestedByUserId,
        CancellationToken ct = default);

    Task<TenantDataRightsRequestDto?> GetAsync(
        Guid tenantId,
        Guid requestId,
        CancellationToken ct = default);

    Task<IReadOnlyList<TenantDataRightsRequestDto>> ListAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task<DataRightsExportDownload> DownloadExportAsync(
        Guid tenantId,
        Guid requestId,
        CancellationToken ct = default);

    Task<TenantDataRightsRequestDto> ConfirmDeleteAsync(
        Guid tenantId,
        Guid requestId,
        string? confirmedByUserId,
        CancellationToken ct = default);

    Task<DeletionResult> ExecuteDeleteAsync(
        Guid tenantId,
        Guid requestId,
        string? actorUserId,
        CancellationToken ct = default);

    /// <summary>Processes pending/failed export requests whose deadline has not expired.</summary>
    Task<int> ProcessPendingExportsAsync(CancellationToken ct = default);
}
