using KasseAPI_Final.Authorization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin SaaS subscription invoices (non-fiscal).</summary>
[ApiController]
[Route("api/admin/invoices")]
[Authorize(Roles = Roles.SuperAdmin)]
[Produces("application/json")]
public sealed class AdminSubscriptionInvoicesController : ControllerBase
{
    private readonly ISubscriptionInvoiceService _invoices;

    public AdminSubscriptionInvoicesController(ISubscriptionInvoiceService invoices)
    {
        _invoices = invoices;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionInvoiceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubscriptionInvoiceDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? status = null,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var items = await _invoices
            .ListAsync(page, pageSize, status, tenantId, fromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SubscriptionInvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionInvoiceDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _invoices.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpGet("{id:guid}/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPdf(Guid id, CancellationToken cancellationToken)
    {
        var pdf = await _invoices.GetPdfAsync(id, cancellationToken).ConfigureAwait(false);
        if (pdf == null)
            return NotFound();

        var meta = await _invoices.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        var fileName = $"{meta?.InvoiceNumber ?? id.ToString("N")}.pdf";
        return File(pdf, "application/pdf", fileName);
    }

    /// <summary>Manually trigger monthly invoice generation (also run by hosted service).</summary>
    [HttpPost("generate-monthly")]
    [ProducesResponseType(typeof(MonthlyInvoiceGenerationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<MonthlyInvoiceGenerationResult>> GenerateMonthly(
        CancellationToken cancellationToken)
    {
        var result = await _invoices.GenerateMonthlyInvoicesAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost("{id:guid}/mark-paid")]
    [ProducesResponseType(typeof(SubscriptionInvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsPaid(
        Guid id,
        [FromBody] MarkPaidRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveActorUserGuid(out var error, out var actorUserId))
            return error!;

        var result = await _invoices
            .MarkPaidAsync(id, request ?? new MarkPaidRequest(), actorUserId, cancellationToken)
            .ConfigureAwait(false);
        return MapActionResult(result);
    }

    [HttpPost("{id:guid}/void")]
    [ProducesResponseType(typeof(SubscriptionInvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VoidInvoice(
        Guid id,
        [FromBody] VoidInvoiceRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveActorUserGuid(out var error, out var actorUserId))
            return error!;

        var result = await _invoices
            .VoidAsync(id, request ?? new VoidInvoiceRequest(), actorUserId, cancellationToken)
            .ConfigureAwait(false);
        return MapActionResult(result);
    }

    private IActionResult MapActionResult(SubscriptionInvoiceActionResult result)
    {
        if (result.Succeeded && result.Invoice != null)
            return Ok(result.Invoice);

        return result.Code switch
        {
            SubscriptionInvoiceService.NotFoundCode => NotFound(),
            SubscriptionInvoiceService.AlreadyPaidCode
                or SubscriptionInvoiceService.AlreadyVoidCode
                or SubscriptionInvoiceService.PaidCannotVoidCode
                or SubscriptionInvoiceService.InvalidStatusCode
                or SubscriptionInvoiceService.ValidationCode =>
                BadRequest(new { message = result.Error, code = result.Code }),
            _ => BadRequest(new { message = result.Error ?? "Request failed.", code = result.Code }),
        };
    }

    private bool TryResolveActorUserGuid(out IActionResult? errorResult, out Guid actorUserId)
    {
        actorUserId = Guid.Empty;
        errorResult = null;

        var userId = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out actorUserId))
        {
            errorResult = Unauthorized(new { message = "Authenticated user id is required." });
            return false;
        }

        return true;
    }
}
