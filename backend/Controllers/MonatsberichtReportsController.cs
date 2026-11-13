using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/reports/monatsbericht")]
public class MonatsberichtReportsController : ControllerBase
{
    private readonly IMonatsberichtService _service;

    public MonatsberichtReportsController(IMonatsberichtService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(AppPermissions.ReportView)]
    [ProducesResponseType(typeof(IReadOnlyList<MonatsberichtListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MonatsberichtListItemDto>>> List(
        [FromQuery] DateTime? fromMonth,
        [FromQuery] DateTime? toMonth,
        [FromQuery] string? scopeKind,
        [FromQuery] Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var data = await _service.ListAsync(fromMonth, toMonth, scopeKind, cashRegisterId, cancellationToken);
        return Ok(data);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.ReportView)]
    [ProducesResponseType(typeof(MonatsberichtDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MonatsberichtDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var row = await _service.GetByIdAsync(id, cancellationToken);
        if (row == null)
            return NotFound();
        return Ok(row);
    }

    [HttpPost("generate")]
    [HasPermission(AppPermissions.ReportExport)]
    [ProducesResponseType(typeof(MonatsberichtDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MonatsberichtDto>> Generate(
        [FromBody] MonatsberichtGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId() ?? "unknown";
        var dto = await _service.GenerateOrRefreshProvisionalAsync(request, userId, cancellationToken);
        return Ok(dto);
    }

    [HttpPost("finalize")]
    [HasPermission(AppPermissions.ReportExport)]
    [ProducesResponseType(typeof(MonatsberichtDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MonatsberichtDto>> Finalize(
        [FromBody] MonatsberichtFinalizeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId() ?? "unknown";
        var dto = await _service.FinalizeAsync(request, userId, cancellationToken);
        return Ok(dto);
    }

    [HttpPost("correction")]
    [HasPermission(AppPermissions.ReportExport)]
    [ProducesResponseType(typeof(MonatsberichtDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MonatsberichtDto>> Correction(
        [FromBody] MonatsberichtCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId() ?? "unknown";
        var dto = await _service.CreateCorrectionAsync(request, userId, cancellationToken);
        return Ok(dto);
    }

    [HttpPost("{id:guid}/submit-finanzonline")]
    [HasPermission(AppPermissions.FinanzOnlineSubmit)]
    [ProducesResponseType(typeof(MonatsberichtDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MonatsberichtDto>> SubmitFinanzOnline(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId() ?? "unknown";
        var dto = await _service.SubmitToFinanzOnlineAsync(id, userId, cancellationToken);
        return Ok(dto);
    }
}
