using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Fiskaly/RKSV receipt operations with structured error envelopes.
/// Manager: own ambient tenant only (cross-tenant cash register → HTTP 404).
/// SuperAdmin: any selected ambient tenant. Synthetic NORMAL is TEST-env only.
/// Storno and Sonderbelege use the canonical fiscal services (not a parallel Fiskaly-only path).
/// Hidden from OpenAPI to avoid Orval churn; FA uses a hand-written client.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/fiskaly/receipt")]
[Produces("application/json")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class FiskalyReceiptController : ControllerBase
{
    private readonly IFiskalyReceiptService _receipts;

    public FiskalyReceiptController(IFiskalyReceiptService receipts)
    {
        _receipts = receipts;
    }

    [HttpPost("normal")]
    [HasPermission(AppPermissions.FiskalyOperationsNormal)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FiskalyReceiptEnvelopeDto>> CreateNormal(
        [FromBody] FiskalyNormalReceiptRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return ValidationFail("Request body is required.");

        var result = await _receipts
            .CreateNormalReceiptAsync(
                request.CashRegisterId,
                request.Amount,
                request.VatRate,
                ActorId(),
                User.IsInRole(Roles.SuperAdmin),
                cancellationToken)
            .ConfigureAwait(false);
        return Map(result);
    }

    [HttpPost("cancel")]
    [HasPermission(AppPermissions.FiskalyOperationsCancel)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FiskalyReceiptEnvelopeDto>> Cancel(
        [FromBody] FiskalyCancelReceiptRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return ValidationFail("Request body is required.");
        if (!ModelState.IsValid)
            return ValidationFail("Invalid cancellation request.");

        var result = await _receipts
            .CancelReceiptAsync(
                request.CashRegisterId,
                request.OriginalReceiptId,
                request.Reason,
                ActorId(),
                User.IsInRole(Roles.SuperAdmin),
                cancellationToken)
            .ConfigureAwait(false);
        return Map(result);
    }

    [HttpPost("nullbeleg")]
    [HasPermission(AppPermissions.FiskalyOperationsNullbeleg)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FiskalyReceiptEnvelopeDto>> CreateNullbeleg(
        [FromBody] FiskalyNullbelegReceiptRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return ValidationFail("Request body is required.");

        var result = await _receipts
            .CreateNullbelegAsync(
                request.CashRegisterId,
                request.Year,
                request.Month,
                request.Reason,
                ActorId(),
                cancellationToken)
            .ConfigureAwait(false);
        return Map(result);
    }

    [HttpPost("startbeleg")]
    [HasPermission(AppPermissions.FiskalyOperationsStartbeleg)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FiskalyReceiptEnvelopeDto>> CreateStartbeleg(
        [FromBody] FiskalyCashRegisterReceiptRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return ValidationFail("Request body is required.");

        var result = await _receipts
            .CreateStartbelegAsync(request.CashRegisterId, request.Reason, ActorId(), cancellationToken)
            .ConfigureAwait(false);
        return Map(result);
    }

    [HttpPost("monatsbeleg")]
    [HasPermission(AppPermissions.FiskalyOperationsMonatsbeleg)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FiskalyReceiptEnvelopeDto>> CreateMonatsbeleg(
        [FromBody] FiskalyMonatsbelegReceiptRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return ValidationFail("Request body is required.");
        if (!ModelState.IsValid)
            return ValidationFail("Year and month are required for Monatsbeleg.");

        var result = await _receipts
            .CreateMonatsbelegAsync(
                request.CashRegisterId,
                request.Year,
                request.Month,
                request.Reason,
                ActorId(),
                User.IsInRole(Roles.SuperAdmin),
                cancellationToken)
            .ConfigureAwait(false);
        return Map(result);
    }

    [HttpPost("jahresbeleg")]
    [HasPermission(AppPermissions.FiskalyOperationsJahresbeleg)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FiskalyReceiptEnvelopeDto>> CreateJahresbeleg(
        [FromBody] FiskalyJahresbelegReceiptRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return ValidationFail("Request body is required.");
        if (!ModelState.IsValid)
            return ValidationFail("Year is required for Jahresbeleg.");

        var result = await _receipts
            .CreateJahresbelegAsync(request.CashRegisterId, request.Year, request.Reason, ActorId(), cancellationToken)
            .ConfigureAwait(false);
        return Map(result);
    }

    [HttpPost("schlussbeleg")]
    [HasPermission(AppPermissions.FiskalyOperationsSchlussbeleg)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FiskalyReceiptEnvelopeDto>> CreateSchlussbeleg(
        [FromBody] FiskalyCashRegisterReceiptRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return ValidationFail("Request body is required.");

        var result = await _receipts
            .CreateSchlussbelegAsync(request.CashRegisterId, request.Reason, ActorId(), cancellationToken)
            .ConfigureAwait(false);
        return Map(result);
    }

    /// <summary>
    /// Submit the TSE-signed Daily closing for a cash register and Vienna day to Fiskaly SIGN AT.
    /// Does not create a new closing. Cross-tenant / missing closing → HTTP 404.
    /// </summary>
    [HttpPost("tagesabschluss")]
    [HasPermission(AppPermissions.FiskalyOperationsTagesabschluss)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FiskalyReceiptEnvelopeDto>> CreateTagesabschlussForRegister(
        [FromBody] FiskalyTagesabschlussReceiptRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return ValidationFail("Request body is required.");

        var result = await _receipts
            .CreateTagesabschlussAsync(
                request.CashRegisterId,
                request.ClosingDate,
                ActorId(),
                User.IsInRole(Roles.SuperAdmin),
                cancellationToken)
            .ConfigureAwait(false);
        return Map(result);
    }

    /// <summary>
    /// Submit an existing Daily closing to Fiskaly SIGN AT as a 0.00 NORMAL marker (Tagesabschluss).
    /// Cross-tenant / missing closing → HTTP 404. Local TSE signature is not overwritten.
    /// </summary>
    [HttpPost("tagesabschluss/{closingId:guid}")]
    [HasPermission(AppPermissions.FiskalyOperationsTagesabschluss)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(FiskalyReceiptEnvelopeDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FiskalyReceiptEnvelopeDto>> CreateTagesabschluss(
        Guid closingId,
        CancellationToken cancellationToken)
    {
        var result = await _receipts
            .CreateTagesabschlussAsync(
                closingId,
                ActorId(),
                User.IsInRole(Roles.SuperAdmin),
                cancellationToken)
            .ConfigureAwait(false);
        return Map(result);
    }

    private string ActorId() => User.GetActorUserId() ?? User.Identity?.Name ?? "unknown";

    private ActionResult<FiskalyReceiptEnvelopeDto> ValidationFail(string message) =>
        BadRequest(new FiskalyReceiptEnvelopeDto
        {
            Success = false,
            Error = FiskalyReceiptErrorMapper.FromCode(
                FiskalyReceiptErrorCodes.ValidationError,
                message)
        });

    private ActionResult<FiskalyReceiptEnvelopeDto> Map(FiskalyReceiptOperationResult result)
    {
        var body = new FiskalyReceiptEnvelopeDto
        {
            Success = result.Success,
            Data = result.Data,
            Error = result.Error,
            HistoryId = result.HistoryId
        };

        if (result.Success)
            return Ok(body);

        return result.StatusCode switch
        {
            StatusCodes.Status404NotFound => NotFound(body),
            StatusCodes.Status409Conflict => Conflict(body),
            _ => BadRequest(body)
        };
    }
}
