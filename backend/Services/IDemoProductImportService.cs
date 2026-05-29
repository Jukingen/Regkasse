using KasseAPI_Final.Models.DTOs;

namespace KasseAPI_Final.Services;

public interface IDemoProductImportService
{
    Task<DemoImportCatalogDto> GetCatalogAsync(CancellationToken cancellationToken = default);

    Task<ImportResult> ImportDemoProductsAsync(
        Guid tenantId,
        DemoImportRequest request,
        CancellationToken cancellationToken = default);
}
