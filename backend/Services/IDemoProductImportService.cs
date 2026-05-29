using KasseAPI_Final.Models.DTOs;

namespace KasseAPI_Final.Services;

public interface IDemoProductImportService
{
    Task<DemoImportCatalogDto> GetCatalogAsync(CancellationToken cancellationToken = default);

    Task<ImportResult> ImportDemoProductsAsync(
        Guid tenantId,
        DemoImportRequest request,
        IProgress<DemoImportProgressDto>? progress = null,
        CancellationToken cancellationToken = default);

    Task<byte[]> GetTemplateCsvAsync(CancellationToken cancellationToken = default);

    Task<DemoTemplateValidationResultDto> ValidateTemplateFileAsync(
        Stream stream,
        string fileName,
        int maxPreviewRows = 20,
        CancellationToken cancellationToken = default);

    Task<ImportResult> ImportFromTemplateFileAsync(
        Guid tenantId,
        Stream stream,
        string fileName,
        DemoImportRequest request,
        CancellationToken cancellationToken = default);
}
