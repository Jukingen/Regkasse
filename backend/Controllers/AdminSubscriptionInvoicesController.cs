using KasseAPI_Final.Authorization;
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
        CancellationToken cancellationToken = default)
    {
        var items = await _invoices.ListAsync(page, pageSize, cancellationToken).ConfigureAwait(false);
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
}
