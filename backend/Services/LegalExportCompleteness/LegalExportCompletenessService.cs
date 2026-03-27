using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.Services.LegalExportCompleteness;

public sealed class LegalExportCompletenessService : ILegalExportCompletenessService
{
    private readonly ITagesberichtService _tagesbericht;
    private readonly IMonatsberichtService _monatsbericht;
    private readonly IJahresberichtService _jahresbericht;
    private readonly IOperationalReportingService _operational;

    public LegalExportCompletenessService(
        ITagesberichtService tagesbericht,
        IMonatsberichtService monatsbericht,
        IJahresberichtService jahresbericht,
        IOperationalReportingService operational)
    {
        _tagesbericht = tagesbericht;
        _monatsbericht = monatsbericht;
        _jahresbericht = jahresbericht;
        _operational = operational;
    }

    public async Task<LegalExportCompletenessResultDto?> GetTagesberichtAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dto = await _tagesbericht.GetByIdAsync(id, cancellationToken);
        return dto == null ? null : LegalExportCompletenessEvaluator.EvaluateTagesbericht(dto);
    }

    public async Task<LegalExportCompletenessResultDto?> GetMonatsberichtAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dto = await _monatsbericht.GetByIdAsync(id, cancellationToken);
        return dto == null ? null : LegalExportCompletenessEvaluator.EvaluateMonatsbericht(dto);
    }

    public async Task<LegalExportCompletenessResultDto?> GetJahresberichtAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dto = await _jahresbericht.GetByIdAsync(id, cancellationToken);
        return dto == null ? null : LegalExportCompletenessEvaluator.EvaluateJahresbericht(dto);
    }

    public async Task<LegalExportCompletenessResultDto?> GetPeriodenberichtAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dto = await _operational.GetFrozenPeriodenberichtByIdAsync(id, cancellationToken);
        return dto == null ? null : LegalExportCompletenessEvaluator.EvaluatePeriodenbericht(dto);
    }
}
