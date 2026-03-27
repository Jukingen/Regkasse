using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.Services.LegalExportCompleteness;

public interface ILegalExportCompletenessService
{
    Task<LegalExportCompletenessResultDto?> GetTagesberichtAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LegalExportCompletenessResultDto?> GetMonatsberichtAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LegalExportCompletenessResultDto?> GetJahresberichtAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LegalExportCompletenessResultDto?> GetPeriodenberichtAsync(Guid id, CancellationToken cancellationToken = default);
}
