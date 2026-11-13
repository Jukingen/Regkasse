using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Formal Tagesbericht (günlük rapor): snapshot, finalize, FinanzOnline outbox gönderimi.
/// </summary>
[Authorize]
[ApiController]
[Route("api/reports/tagesbericht")]
public class TagesberichtReportsController : ControllerBase
{
    private readonly ITagesberichtService _service;

    public TagesberichtReportsController(ITagesberichtService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(AppPermissions.ReportView)]
    [ProducesResponseType(typeof(IReadOnlyList<TagesberichtListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TagesberichtListItemDto>>> List(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var data = await _service.ListAsync(fromDate, toDate, cashRegisterId, cancellationToken);
        return Ok(data);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.ReportView)]
    [ProducesResponseType(typeof(TagesberichtDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TagesberichtDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var row = await _service.GetByIdAsync(id, cancellationToken);
        if (row == null)
            return NotFound();
        return Ok(row);
    }

    [HttpPost("generate")]
    [HasPermission(AppPermissions.ReportExport)]
    [ProducesResponseType(typeof(TagesberichtDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TagesberichtDto>> Generate(
        [FromBody] TagesberichtGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId() ?? "unknown";
        var dto = await _service.GenerateOrRefreshProvisionalAsync(request, userId, cancellationToken);
        return Ok(dto);
    }

    [HttpPost("finalize")]
    [HasPermission(AppPermissions.ReportExport)]
    [ProducesResponseType(typeof(TagesberichtDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TagesberichtDto>> Finalize(
        [FromBody] TagesberichtFinalizeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId() ?? "unknown";
        var dto = await _service.FinalizeAsync(request, userId, cancellationToken);
        return Ok(dto);
    }

    [HttpPost("correction")]
    [HasPermission(AppPermissions.ReportExport)]
    [ProducesResponseType(typeof(TagesberichtDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TagesberichtDto>> Correction(
        [FromBody] TagesberichtCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId() ?? "unknown";
        var dto = await _service.CreateCorrectionAsync(request, userId, cancellationToken);
        return Ok(dto);
    }

    [HttpPost("{id:guid}/submit-finanzonline")]
    [HasPermission(AppPermissions.FinanzOnlineSubmit)]
    [ProducesResponseType(typeof(TagesberichtDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TagesberichtDto>> SubmitFinanzOnline(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId() ?? "unknown";
        var dto = await _service.SubmitToFinanzOnlineAsync(id, userId, cancellationToken);
        return Ok(dto);
    }
}
