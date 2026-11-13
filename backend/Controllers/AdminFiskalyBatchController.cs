using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Batch Fiskaly operations (storno, Sonderbelege, SuperAdmin DEP ZIP).
/// Hidden from OpenAPI; FA uses a hand-written client.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/fiskaly/batch")]
[Produces("application/json")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class AdminFiskalyBatchController : ControllerBase
{
    private readonly IFiskalyBatchService _batch;

    public AdminFiskalyBatchController(IFiskalyBatchService batch)
    {
        _batch = batch;
    }

    [HttpGet("limits")]
    [HasPermission(AppPermissions.FiskalyOperationsView)]
    [ProducesResponseType(typeof(FiskalyBatchLimitsDto), StatusCodes.Status200OK)]
    public ActionResult<FiskalyBatchLimitsDto> Limits() => Ok(_batch.GetLimits());

    [HttpPost("storno")]
    [HasPermission(AppPermissions.FiskalyOperationsCancel)]
    [ProducesResponseType(typeof(FiskalyBatchOperationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FiskalyBatchErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FiskalyBatchOperationResultDto>> Storno(
        [FromBody] FiskalyBatchStornoRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BatchFail(FiskalyBatchErrorCodes.BatchValidation, "Request body is required.");

        return await ExecuteAsync(
                () => _batch.StornoAsync(request, ActorId(), User.IsInRole(Roles.SuperAdmin), cancellationToken))
            .ConfigureAwait(false);
    }

    [HttpPost("sonderbelege")]
    [HasPermission(AppPermissions.FiskalyOperationsView)]
    [ProducesResponseType(typeof(FiskalyBatchOperationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FiskalyBatchErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FiskalyBatchOperationResultDto>> Sonderbelege(
        [FromBody] FiskalyBatchSonderbelegeRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BatchFail(FiskalyBatchErrorCodes.BatchValidation, "Request body is required.");

        var kind = (request.Kind ?? string.Empty).Trim().ToLowerInvariant();
        var required = kind switch
        {
            FiskalyOperationTypes.Startbeleg => AppPermissions.FiskalyOperationsStartbeleg,
            FiskalyOperationTypes.Monatsbeleg => AppPermissions.FiskalyOperationsMonatsbeleg,
            FiskalyOperationTypes.Jahresbeleg => AppPermissions.FiskalyOperationsJahresbeleg,
            _ => null
        };
        if (required is null)
            return BatchFail(FiskalyBatchErrorCodes.BatchForbiddenKind, "Unsupported batch Sonderbeleg kind.");
        if (!User.HasPermissionClaim(required))
            return Forbid();

        return await ExecuteAsync(
                () => _batch.SonderbelegeAsync(request, ActorId(), User.IsInRole(Roles.SuperAdmin), cancellationToken))
            .ConfigureAwait(false);
    }

    [HttpPost("dep-export")]
    [HasPermission(AppPermissions.SystemCritical)]
    [HasPermission(AppPermissions.ReportExport)]
    [HasPermission(AppPermissions.AuditView)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FiskalyBatchErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DepExport(
        [FromBody] FiskalyBatchDepExportRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BatchFail(FiskalyBatchErrorCodes.BatchValidation, "Request body is required.");
        if (!User.IsInRole(Roles.SuperAdmin))
            return NotFound();

        try
        {
            var result = await _batch
                .DepExportAsync(request, ActorId(), actorIsSuperAdmin: true, cancellationToken)
                .ConfigureAwait(false);
            Response.Headers["X-Regkasse-Batch-Success"] = result.Summary.SuccessCount.ToString();
            Response.Headers["X-Regkasse-Batch-Failed"] = result.Summary.FailedCount.ToString();
            Response.Headers["X-Regkasse-Batch-Id"] = result.Summary.BatchId.ToString("D");
            return File(result.ZipBytes, "application/zip", result.FileName);
        }
        catch (FiskalyBatchException ex)
        {
            return StatusCode(ex.StatusCode, new FiskalyBatchErrorDto
            {
                Success = false,
                Code = ex.Code,
                Message = ex.Message,
                MaxItems = ex.MaxItems
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private async Task<ActionResult<FiskalyBatchOperationResultDto>> ExecuteAsync(
        Func<Task<FiskalyBatchOperationResultDto>> action)
    {
        try
        {
            return Ok(await action().ConfigureAwait(false));
        }
        catch (FiskalyBatchException ex)
        {
            return StatusCode(ex.StatusCode, new FiskalyBatchErrorDto
            {
                Success = false,
                Code = ex.Code,
                Message = ex.Message,
                MaxItems = ex.MaxItems
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private string ActorId() => User.GetActorUserId() ?? User.Identity?.Name ?? "unknown";

    private BadRequestObjectResult BatchFail(string code, string message) =>
        BadRequest(new FiskalyBatchErrorDto { Success = false, Code = code, Message = message });
}
