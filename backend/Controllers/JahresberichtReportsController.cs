using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/reports/jahresbericht")]
public class JahresberichtReportsController : ControllerBase
{
    private readonly IJahresberichtService _service;

    public JahresberichtReportsController(IJahresberichtService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(AppPermissions.ReportView)]
    [ProducesResponseType(typeof(IReadOnlyList<JahresberichtListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<JahresberichtListItemDto>>> List(
        [FromQuery] DateTime? fromYear,
        [FromQuery] DateTime? toYear,
        [FromQuery] string? scopeKind,
        [FromQuery] Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var data = await _service.ListAsync(fromYear, toYear, scopeKind, cashRegisterId, cancellationToken);
        return Ok(data);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.ReportView)]
    [ProducesResponseType(typeof(JahresberichtDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JahresberichtDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var row = await _service.GetByIdAsync(id, cancellationToken);
        if (row == null)
            return NotFound();
        return Ok(row);
    }

    [HttpPost("generate")]
    [HasPermission(AppPermissions.ReportExport)]
    [ProducesResponseType(typeof(JahresberichtDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<JahresberichtDto>> Generate(
        [FromBody] JahresberichtGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId() ?? "unknown";
        var dto = await _service.GenerateOrRefreshProvisionalAsync(request, userId, cancellationToken);
        return Ok(dto);
    }

    [HttpPost("finalize")]
    [HasPermission(AppPermissions.ReportExport)]
    [ProducesResponseType(typeof(JahresberichtDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<JahresberichtDto>> Finalize(
        [FromBody] JahresberichtFinalizeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId() ?? "unknown";
        var dto = await _service.FinalizeAsync(request, userId, cancellationToken);
        return Ok(dto);
    }

    [HttpPost("correction")]
    [HasPermission(AppPermissions.ReportExport)]
    [ProducesResponseType(typeof(JahresberichtDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<JahresberichtDto>> Correction(
        [FromBody] JahresberichtCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId() ?? "unknown";
        var dto = await _service.CreateCorrectionAsync(request, userId, cancellationToken);
        return Ok(dto);
    }

    [HttpPost("{id:guid}/submit-finanzonline")]
    [HasPermission(AppPermissions.FinanzOnlineSubmit)]
    [ProducesResponseType(typeof(JahresberichtDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<JahresberichtDto>> SubmitFinanzOnline(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId() ?? "unknown";
        var dto = await _service.SubmitToFinanzOnlineAsync(id, userId, cancellationToken);
        return Ok(dto);
    }
}
