using KasseAPI_Final.Authorization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IBillingTenantLicenseService = KasseAPI_Final.Services.Billing.ITenantLicenseService;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin Mandanten license billing (isolated from RKSV / deployment licensing).</summary>
[ApiController]
[Route("api/admin/billing")]
[Authorize(Roles = Roles.SuperAdmin)]
public sealed class AdminBillingController : ControllerBase
{
    private readonly IBillingService _billingService;
    private readonly IBillingTenantLicenseService _tenantLicenseService;
    private readonly IInvoicePdfGenerator _pdfGenerator;
    private readonly IBillingAuditService _auditService;
    private readonly IReminderService _reminderService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AdminBillingController> _logger;

    public AdminBillingController(
        IBillingService billingService,
        IBillingTenantLicenseService tenantLicenseService,
        IInvoicePdfGenerator pdfGenerator,
        IBillingAuditService auditService,
        IReminderService reminderService,
        ICurrentUserService currentUserService,
        ILogger<AdminBillingController> logger)
    {
        _billingService = billingService;
        _tenantLicenseService = tenantLicenseService;
        _pdfGenerator = pdfGenerator;
        _auditService = auditService;
        _reminderService = reminderService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    #region Preview & Create

    /// <summary>Preview a license sale before creating it.</summary>
    [HttpPost("license-sales/preview")]
    [ProducesResponseType(typeof(LicenseSalePreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewLicenseSale(
        [FromBody] LicenseSalePreviewRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _billingService.PreviewLicenseSaleAsync(request, ct).ConfigureAwait(false);
            return Ok(result);
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

    /// <summary>Create a new license sale (Super Admin only).</summary>
    [HttpPost("license-sales")]
    [ProducesResponseType(typeof(LicenseSaleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateLicenseSale(
        [FromBody] CreateLicenseSaleRequest request,
        CancellationToken ct)
    {
        if (!TryResolveActorUserGuid(out var errorResult, out var userId))
            return errorResult!;

        try
        {
            var result = await _billingService.CreateLicenseSaleAsync(request, userId, ct).ConfigureAwait(false);

            try
            {
                await _billingService.GenerateInvoicePdfAsync(result.Id, ct).ConfigureAwait(false);
                _logger.LogInformation("PDF generated for sale {SaleId}", result.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate PDF for sale {SaleId}", result.Id);
            }

            return CreatedAtAction(nameof(GetLicenseSale), new { id = result.Id }, result);
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

    #endregion

    #region Read

    /// <summary>Get a single license sale by ID.</summary>
    [HttpGet("license-sales/{id:guid}")]
    [ProducesResponseType(typeof(LicenseSaleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLicenseSale(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        try
        {
            var result = await _billingService.GetLicenseSaleAsync(id, ct).ConfigureAwait(false);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Sale {id} not found" });
        }
    }

    /// <summary>List license sales with pagination and filters.</summary>
    [HttpGet("license-sales")]
    [ProducesResponseType(typeof(LicenseSaleListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListLicenseSales(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var query = new LicenseSaleListQuery
        {
            Page = page,
            PageSize = Math.Min(pageSize, 100),
            TenantId = tenantId,
            Status = status,
            FromDate = fromDate,
            ToDate = toDate,
            Search = search,
        };

        try
        {
            var result = await _billingService.ListLicenseSalesAsync(query, ct).ConfigureAwait(false);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Get license sale statistics.</summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(LicenseSaleStatsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var result = await _billingService
            .GetLicenseSaleStatsAsync(fromDate, toDate, ct)
            .ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Get a sale by license key.</summary>
    [HttpGet("license-sales/by-key/{licenseKey}")]
    [ProducesResponseType(typeof(LicenseSaleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSaleByLicenseKey(
        [FromRoute] string licenseKey,
        CancellationToken ct)
    {
        var result = await _billingService.GetSaleByLicenseKeyAsync(licenseKey, ct).ConfigureAwait(false);
        if (result is null)
            return NotFound(new { message = $"License key {licenseKey} not found" });

        return Ok(result);
    }

    /// <summary>Get license information for a specific tenant.</summary>
    [HttpGet("tenants/{tenantId:guid}/license")]
    [ProducesResponseType(typeof(TenantLicenseInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenantLicense(
        [FromRoute] Guid tenantId,
        CancellationToken ct)
    {
        try
        {
            var result = await _tenantLicenseService.GetLicenseInfoAsync(tenantId, ct).ConfigureAwait(false);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Tenant {tenantId} not found" });
        }
    }

    /// <summary>Get expiring licenses (for reminders).</summary>
    [HttpGet("license-sales/expiring")]
    [ProducesResponseType(typeof(List<ExpiringLicenseInfo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpiringLicenses(
        [FromQuery] int daysThreshold = 30,
        CancellationToken ct = default)
    {
        var result = await _tenantLicenseService
            .GetExpiringLicensesAsync(daysThreshold, ct)
            .ConfigureAwait(false);
        return Ok(result);
    }

    #endregion

    #region PDF

    /// <summary>Download PDF invoice for a license sale.</summary>
    [HttpGet("license-sales/{id:guid}/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadPdf(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        try
        {
            var sale = await _billingService.GetLicenseSaleAsync(id, ct).ConfigureAwait(false);
            var pdfBytes = await _pdfGenerator.GenerateInvoicePdfAsync(id, ct).ConfigureAwait(false);
            var fileName = $"RE-{sale.InvoiceNumber}-{sale.TenantSlug}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Sale {id} not found" });
        }
    }

    /// <summary>Generate preview PDF for a license sale (without saving).</summary>
    [HttpPost("license-sales/preview-pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewPdf(
        [FromBody] LicenseSalePreviewRequest request,
        CancellationToken ct)
    {
        try
        {
            var preview = await _billingService.PreviewLicenseSaleAsync(request, ct).ConfigureAwait(false);
            var pdfBytes = await _pdfGenerator.GeneratePreviewPdfAsync(preview, ct).ConfigureAwait(false);

            return File(pdfBytes, "application/pdf", $"Vorschau-{preview.TenantSlug}.pdf");
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

    #endregion

    #region Cancel & Manage

    /// <summary>Cancel a license sale.</summary>
    [HttpPost("license-sales/{id:guid}/cancel")]
    [ProducesResponseType(typeof(LicenseSaleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelLicenseSale(
        [FromRoute] Guid id,
        [FromBody] CancelLicenseSaleRequest request,
        CancellationToken ct)
    {
        if (!TryResolveActorUserGuid(out var errorResult, out var userId))
            return errorResult!;

        try
        {
            var result = await _billingService
                .CancelLicenseSaleAsync(id, request, userId, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Sale {id} not found" });
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

    #endregion

    #region Audit

    /// <summary>Get billing audit logs.</summary>
    [HttpGet("audit")]
    [ProducesResponseType(typeof(BillingAuditLogListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] Guid? saleId = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? userId = null,
        CancellationToken ct = default)
    {
        var query = new BillingAuditLogQuery
        {
            Page = page,
            PageSize = Math.Min(pageSize, 100),
            TenantId = tenantId,
            SaleId = saleId,
            Action = action,
            FromDate = fromDate,
            ToDate = toDate,
            UserId = userId,
        };

        var result = await _auditService.ListAsync(query, ct).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Get audit logs for a specific sale.</summary>
    [HttpGet("license-sales/{id:guid}/audit")]
    [ProducesResponseType(typeof(List<BillingAuditLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSaleAuditLogs(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        try
        {
            var result = await _auditService.GetForSaleAsync(id, ct).ConfigureAwait(false);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Sale {id} not found" });
        }
    }

    #endregion

    #region Reminders

    /// <summary>Get reminders for a specific tenant.</summary>
    [HttpGet("tenants/{tenantId:guid}/reminders")]
    [ProducesResponseType(typeof(List<LicenseReminderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenantReminders(
        [FromRoute] Guid tenantId,
        CancellationToken ct)
    {
        try
        {
            var result = await _reminderService.GetForTenantAsync(tenantId, ct).ConfigureAwait(false);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Tenant {tenantId} not found" });
        }
    }

    /// <summary>Trigger reminder creation (admin manual trigger).</summary>
    [HttpPost("reminders/check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TriggerReminderCheck(CancellationToken ct)
    {
        await _reminderService.CheckAndCreateRemindersAsync(ct).ConfigureAwait(false);
        return Ok(new { message = "Reminder check completed" });
    }

    /// <summary>Send pending reminders (admin manual trigger).</summary>
    [HttpPost("reminders/send")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TriggerReminderSend(CancellationToken ct)
    {
        await _reminderService.SendPendingRemindersAsync(ct).ConfigureAwait(false);
        return Ok(new { message = "Reminders sent" });
    }

    #endregion

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
