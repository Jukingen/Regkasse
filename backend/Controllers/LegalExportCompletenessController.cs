using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.LegalExportCompleteness;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// LegalComplianceExport öncesi bütünlük kapısı — aynı izinlerle rapor detayı okunur.
/// </summary>
[Authorize]
[ApiController]
[Route("api/reports/legal-export-completeness")]
public class LegalExportCompletenessController : ControllerBase
{
    private readonly ILegalExportCompletenessService _completeness;

    public LegalExportCompletenessController(ILegalExportCompletenessService completeness)
    {
        _completeness = completeness;
    }

    [HttpGet("tagesbericht/{id:guid}")]
    [HasPermission(AppPermissions.ReportView)]
    [ProducesResponseType(typeof(LegalExportCompletenessResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LegalExportCompletenessResultDto>> GetTagesbericht(Guid id, CancellationToken cancellationToken)
    {
        var result = await _completeness.GetTagesberichtAsync(id, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("monatsbericht/{id:guid}")]
    [HasPermission(AppPermissions.ReportView)]
    [ProducesResponseType(typeof(LegalExportCompletenessResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LegalExportCompletenessResultDto>> GetMonatsbericht(Guid id, CancellationToken cancellationToken)
    {
        var result = await _completeness.GetMonatsberichtAsync(id, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("jahresbericht/{id:guid}")]
    [HasPermission(AppPermissions.ReportView)]
    [ProducesResponseType(typeof(LegalExportCompletenessResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LegalExportCompletenessResultDto>> GetJahresbericht(Guid id, CancellationToken cancellationToken)
    {
        var result = await _completeness.GetJahresberichtAsync(id, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("periodenbericht/{id:guid}")]
    [HasPermission(AppPermissions.ReportView)]
    [ProducesResponseType(typeof(LegalExportCompletenessResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LegalExportCompletenessResultDto>> GetPeriodenbericht(Guid id, CancellationToken cancellationToken)
    {
        var result = await _completeness.GetPeriodenberichtAsync(id, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }
}
