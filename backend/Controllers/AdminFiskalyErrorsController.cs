using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Fiskaly error analysis. Manager: ambient tenant only. SuperAdmin: all tenants
/// (optional <c>tenantId</c>). Cross-tenant review → HTTP 404. Hidden from OpenAPI.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/fiskaly/errors")]
[Produces("application/json")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class AdminFiskalyErrorsController : ControllerBase
{
    private readonly IFiskalyErrorService _errors;

    public AdminFiskalyErrorsController(IFiskalyErrorService errors)
    {
        _errors = errors;
    }

    [HttpGet]
    [HasPermission(AppPermissions.FiskalyHistoryView)]
    [ProducesResponseType(typeof(PagedResult<FiskalyErrorListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<FiskalyErrorListItemDto>>> List(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? operationType,
        [FromQuery] Guid? tenantId,
        [FromQuery] string? reviewStatus,
        [FromQuery] string? errorCode,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dto = await _errors
                .ListAsync(
                    ToQuery(fromUtc, toUtc, operationType, tenantId, reviewStatus, errorCode, search, page, pageSize),
                    User.IsInRole(Roles.SuperAdmin),
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, code = "ERRORS_VALIDATION", message = ex.Message });
        }
    }

    [HttpGet("stats")]
    [HasPermission(AppPermissions.FiskalyHistoryView)]
    [ProducesResponseType(typeof(FiskalyErrorStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FiskalyErrorStatsDto>> Stats(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? operationType,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dto = await _errors
                .GetStatsAsync(
                    ToQuery(fromUtc, toUtc, operationType, tenantId, null, null, null, 1, 25),
                    User.IsInRole(Roles.SuperAdmin),
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, code = "ERRORS_VALIDATION", message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.FiskalyHistoryView)]
    [ProducesResponseType(typeof(FiskalyErrorDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FiskalyErrorDetailDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var row = await _errors
            .GetByIdAsync(id, User.IsInRole(Roles.SuperAdmin), cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return NotFound();
        return Ok(row);
    }

    [HttpGet("export")]
    [HasPermission(AppPermissions.FiskalyHistoryView)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Export(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? operationType,
        [FromQuery] Guid? tenantId,
        [FromQuery] string? reviewStatus,
        [FromQuery] string? errorCode,
        [FromQuery] string? search,
        [FromQuery] string format = "csv",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _errors
                .ExportAsync(
                    ToQuery(fromUtc, toUtc, operationType, tenantId, reviewStatus, errorCode, search, 1, FiskalyErrorService.MaxExportRows),
                    format,
                    User.IsInRole(Roles.SuperAdmin),
                    cancellationToken)
                .ConfigureAwait(false);
            return File(result.Bytes, result.ContentType, result.FileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, code = "ERRORS_VALIDATION", message = ex.Message });
        }
    }

    [HttpPut("{id:guid}/resolve")]
    [HasPermission(AppPermissions.FiskalyHistoryRetry)]
    [ProducesResponseType(typeof(FiskalyErrorListItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FiskalyErrorListItemDto>> Resolve(
        Guid id,
        [FromBody] FiskalyErrorReviewRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _errors
                .SetReviewStatusAsync(
                    id,
                    body.ReviewStatus,
                    User.GetActorUserId() ?? User.Identity?.Name ?? "unknown",
                    User.IsInRole(Roles.SuperAdmin),
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, code = "ERRORS_VALIDATION", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, code = "ERRORS_VALIDATION", message = ex.Message });
        }
    }

    private static FiskalyErrorQuery ToQuery(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? operationType,
        Guid? tenantId,
        string? reviewStatus,
        string? errorCode,
        string? search,
        int page,
        int pageSize) =>
        new()
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            OperationType = operationType,
            TenantId = tenantId,
            ReviewStatus = reviewStatus,
            ErrorCode = errorCode,
            Search = search,
            Page = page,
            PageSize = pageSize
        };
}
