using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Fiskaly operation history for FA. Manager: own ambient tenant only.
/// SuperAdmin: all tenants (optional <c>tenantId</c> filter). Cross-tenant GET/retry → HTTP 404.
/// Hidden from OpenAPI; FA uses a hand-written client (same as receipt operations).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/fiskaly/history")]
[Produces("application/json")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class AdminFiskalyHistoryController : ControllerBase
{
    private readonly IFiskalyOperationHistoryService _history;

    public AdminFiskalyHistoryController(IFiskalyOperationHistoryService history)
    {
        _history = history;
    }

    [HttpGet]
    [HasPermission(AppPermissions.FiskalyHistoryView)]
    [ProducesResponseType(typeof(PagedResult<FiskalyOperationHistoryListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<FiskalyOperationHistoryListItemDto>>> List(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? operationType,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] Guid? tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _history
            .ListAsync(
                new FiskalyOperationHistoryQuery
                {
                    FromUtc = fromUtc,
                    ToUtc = toUtc,
                    OperationType = operationType,
                    Status = status,
                    Search = search,
                    TenantId = tenantId,
                    Page = page,
                    PageSize = pageSize
                },
                User.IsInRole(Roles.SuperAdmin),
                cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.FiskalyHistoryView)]
    [ProducesResponseType(typeof(FiskalyOperationHistoryDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FiskalyOperationHistoryDetailDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var row = await _history
            .GetByIdAsync(id, User.IsInRole(Roles.SuperAdmin), cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return NotFound();
        return Ok(row);
    }

    [HttpPost("{id:guid}/retry")]
    [HasPermission(AppPermissions.FiskalyHistoryRetry)]
    [ProducesResponseType(typeof(FiskalyOperationHistoryRetryResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FiskalyOperationHistoryRetryResultDto>> Retry(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _history
                .RetryAsync(
                    id,
                    User.GetActorUserId() ?? User.Identity?.Name ?? "unknown",
                    User.IsInRole(Roles.SuperAdmin),
                    cancellationToken)
                .ConfigureAwait(false);

            if (result.Operation.Success)
                return Ok(result);

            return BadRequest(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new FiskalyReceiptEnvelopeDto
            {
                Success = false,
                Error = FiskalyReceiptErrorMapper.FromCode(
                    FiskalyReceiptErrorCodes.ValidationError,
                    ex.Message)
            });
        }
    }
}
