using KasseAPI_Final.Authorization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin Mandanten license billing (isolated from RKSV / deployment licensing).</summary>
[ApiController]
[Route("api/admin/billing")]
[Authorize(Roles = Roles.SuperAdmin)]
[Produces("application/json")]
public sealed class AdminBillingController : ControllerBase
{
    private readonly IBillingService _billingService;
    private readonly ILogger<AdminBillingController> _logger;

    public AdminBillingController(
        IBillingService billingService,
        ILogger<AdminBillingController> logger)
    {
        _billingService = billingService;
        _logger = logger;
    }

    /// <summary>Preview license sale pricing, validity, invoice number, and generated billing key.</summary>
    [HttpPost("license-sales/preview")]
    [ProducesResponseType(typeof(LicenseSalePreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewLicenseSale(
        [FromBody] LicenseSalePreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var preview = await _billingService
                .PreviewLicenseSaleAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return Ok(preview);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Record a license sale, update tenant mandant license, and return the persisted sale.</summary>
    [HttpPost("license-sales")]
    [ProducesResponseType(typeof(LicenseSaleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateLicenseSale(
        [FromBody] CreateLicenseSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveActorUserGuid(out var errorResult, out var actorUserId))
            return errorResult!;

        try
        {
            var sale = await _billingService
                .CreateLicenseSaleAsync(request, actorUserId, cancellationToken)
                .ConfigureAwait(false);
            return CreatedAtAction(nameof(GetLicenseSale), new { id = sale.Id }, sale);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>List license sales with pagination and optional filters.</summary>
    [HttpGet("license-sales")]
    [ProducesResponseType(typeof(LicenseSaleListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListLicenseSales(
        [FromQuery] LicenseSaleListQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _billingService
                .ListLicenseSalesAsync(query, cancellationToken)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Get a single license sale by id.</summary>
    [HttpGet("license-sales/{id:guid}")]
    [ProducesResponseType(typeof(LicenseSaleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLicenseSale(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sale = await _billingService
                .GetLicenseSaleAsync(id, cancellationToken)
                .ConfigureAwait(false);
            return Ok(sale);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Download the license sale invoice PDF (non-RKSV billing document).</summary>
    [HttpGet("license-sales/{id:guid}/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadPdf(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sale = await _billingService.GetLicenseSaleAsync(id, cancellationToken).ConfigureAwait(false);
            var pdf = await _billingService.GenerateInvoicePdfAsync(id, cancellationToken).ConfigureAwait(false);
            var fileName = $"{sale.InvoiceNumber}.pdf";
            return File(pdf, "application/pdf", fileName);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogInformation(ex, "License sale PDF not found for sale {SaleId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Cancel an active license sale.</summary>
    [HttpPost("license-sales/{id:guid}/cancel")]
    [ProducesResponseType(typeof(LicenseSaleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelLicenseSale(
        Guid id,
        [FromBody] CancelLicenseSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveActorUserGuid(out var errorResult, out var actorUserId))
            return errorResult!;

        try
        {
            var sale = await _billingService
                .CancelLicenseSaleAsync(id, request, actorUserId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(sale);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Aggregate billing stats for active license sales.</summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(LicenseSaleStatsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var stats = await _billingService
            .GetLicenseSaleStatsAsync(fromDate, toDate, cancellationToken)
            .ConfigureAwait(false);
        return Ok(stats);
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
