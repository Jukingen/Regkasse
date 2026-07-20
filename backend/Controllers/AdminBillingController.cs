using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.DigitalServices;
using KasseAPI_Final.Services.License;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IBillingTenantLicenseService = KasseAPI_Final.Services.Billing.ITenantLicenseService;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin Mandanten license billing (isolated from RKSV / deployment licensing).</summary>
[ApiController]
[Route("api/admin/billing")]
[Authorize]
[HasPermission(AppPermissions.SystemCritical)]
public sealed class AdminBillingController : ControllerBase
{
    private readonly IBillingService _billingService;
    private readonly IBillingTenantLicenseService _tenantLicenseService;
    private readonly IInvoicePdfGenerator _pdfGenerator;
    private readonly IBillingAuditService _auditService;
    private readonly IReminderService _reminderService;
    private readonly ILicenseReminderService _licenseReminderService;
    private readonly IBillingBackupService _backupService;
    private readonly IDigitalServicePricingService _digitalPricing;
    private readonly ISubscriptionService _subscriptions;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AdminBillingController> _logger;

    public AdminBillingController(
        IBillingService billingService,
        IBillingTenantLicenseService tenantLicenseService,
        IInvoicePdfGenerator pdfGenerator,
        IBillingAuditService auditService,
        IReminderService reminderService,
        ILicenseReminderService licenseReminderService,
        IBillingBackupService backupService,
        IDigitalServicePricingService digitalPricing,
        ISubscriptionService subscriptions,
        ICurrentUserService currentUserService,
        ILogger<AdminBillingController> logger)
    {
        _billingService = billingService;
        _tenantLicenseService = tenantLicenseService;
        _pdfGenerator = pdfGenerator;
        _auditService = auditService;
        _reminderService = reminderService;
        _licenseReminderService = licenseReminderService;
        _backupService = backupService;
        _digitalPricing = digitalPricing;
        _subscriptions = subscriptions;
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

    /// <summary>Digital service list prices (website / app). Catalog only — not license_sales.</summary>
    [HttpGet("digital-pricing")]
    [ProducesResponseType(typeof(IReadOnlyList<ServicePricingDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ServicePricingDto>> GetDigitalPricing([FromQuery] string? type = null)
    {
        var items = _digitalPricing.GetPricing(type)
            .Select(p => new ServicePricingDto
            {
                ServiceId = p.ServiceId,
                Name = p.Name,
                Type = p.Type,
                Tier = p.Tier,
                PriceMonthly = p.PriceMonthly,
                PriceYearly = p.PriceYearly,
                Features = p.Features,
                Currency = p.Currency
            })
            .ToList();
        return Ok(items);
    }

    /// <summary>
    /// Cross-tenant digital-service MRR dashboard (active price snapshots). Not license_sales.
    /// </summary>
    [HttpGet("digital")]
    [ProducesResponseType(typeof(DigitalBillingDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DigitalBillingDashboardDto>> GetDigitalBillingDashboard(
        CancellationToken ct)
    {
        var dashboard = await _subscriptions.GetDigitalBillingDashboardAsync(ct);
        return Ok(dashboard);
    }

    /// <summary>Create a digital-service subscription for a tenant (list price snapshot; no payment capture).</summary>
    [HttpPost("subscriptions")]
    [ProducesResponseType(typeof(SubscriptionMutationResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(SubscriptionMutationResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(SubscriptionMutationResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionMutationResponseDto>> CreateSubscription(
        [FromBody] CreateSubscriptionRequestDto? body,
        CancellationToken ct)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.ServiceId) || body.TenantId == Guid.Empty)
        {
            return BadRequest(new SubscriptionMutationResponseDto
            {
                Succeeded = false,
                Code = "VALIDATION_ERROR",
                Error = "TenantId and ServiceId are required."
            });
        }

        var actor = User.GetActorUserId();
        var result = await _subscriptions.CreateSubscriptionAsync(
            body.TenantId,
            body.ServiceId,
            actor,
            ct);

        var dto = MapSubscriptionMutation(result);
        if (!result.Succeeded)
        {
            return result.Code switch
            {
                SubscriptionService.TenantNotFoundCode => NotFound(dto),
                SubscriptionService.ServiceNotFoundCode => BadRequest(dto),
                SubscriptionService.AlreadyActiveCode => Conflict(dto),
                _ => BadRequest(dto)
            };
        }

        return CreatedAtAction(
            nameof(ListTenantSubscriptions),
            new { tenantId = body.TenantId },
            dto);
    }

    /// <summary>List digital-service subscriptions for a tenant.</summary>
    [HttpGet("tenants/{tenantId:guid}/subscriptions")]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubscriptionResponseDto>>> ListTenantSubscriptions(
        [FromRoute] Guid tenantId,
        CancellationToken ct)
    {
        var items = await _subscriptions.ListForTenantAsync(tenantId, ct);
        return Ok(items.Select(MapSubscription).ToList());
    }

    /// <summary>Cancel a digital-service subscription.</summary>
    [HttpPost("subscriptions/{subscriptionId:guid}/cancel")]
    [ProducesResponseType(typeof(SubscriptionMutationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SubscriptionMutationResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionMutationResponseDto>> CancelSubscription(
        [FromRoute] Guid subscriptionId,
        CancellationToken ct)
    {
        var actor = User.GetActorUserId();
        var result = await _subscriptions.CancelSubscriptionAsync(subscriptionId, actor, ct);
        var dto = MapSubscriptionMutation(result);
        if (!result.Succeeded)
        {
            return result.Code switch
            {
                SubscriptionService.NotFoundCode => NotFound(dto),
                SubscriptionService.AlreadyCancelledCode => BadRequest(dto),
                _ => BadRequest(dto)
            };
        }

        return Ok(dto);
    }

    private static SubscriptionMutationResponseDto MapSubscriptionMutation(SubscriptionResult result) =>
        new()
        {
            Succeeded = result.Succeeded,
            Code = result.Code,
            Error = result.Error,
            Subscription = result.Subscription is null ? null : MapSubscription(result.Subscription)
        };

    private static SubscriptionResponseDto MapSubscription(KasseAPI_Final.Models.Subscription s) =>
        new()
        {
            Id = s.Id,
            TenantId = s.TenantId,
            ServiceId = s.ServiceId,
            Price = s.Price,
            Currency = s.Currency,
            Status = s.Status,
            CreatedAt = s.CreatedAt,
            NextBillingDate = s.NextBillingDate,
            CancelledAtUtc = s.CancelledAtUtc
        };

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
        var billingSent = await _licenseReminderService.SendDueBillingSaleRemindersAsync(ct).ConfigureAwait(false);
        var mandantResult = await _licenseReminderService.SendDueMandantExpiryRemindersAsync(ct).ConfigureAwait(false);
        return Ok(new
        {
            message = "Reminders sent",
            billingEmailsSent = billingSent,
            mandantEmailsSent = mandantResult.EmailsSent,
        });
    }

    #endregion

    #region Backup

    /// <summary>List billing backup history.</summary>
    [HttpGet("backup/history")]
    [ProducesResponseType(typeof(BackupHistoryListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetBackupHistory(
        [FromQuery] BackupHistoryQuery query,
        CancellationToken ct)
    {
        try
        {
            var result = await _backupService.ListBackupHistoryAsync(query, ct).ConfigureAwait(false);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Get billing backup details.</summary>
    [HttpGet("backup/{id:guid}")]
    [ProducesResponseType(typeof(BackupHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBackupDetails(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        try
        {
            var result = await _backupService.GetBackupDetailsAsync(id, ct).ConfigureAwait(false);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Backup {id} not found" });
        }
    }

    /// <summary>Download a billing backup archive.</summary>
    [HttpGet("backup/{id:guid}/download")]
    [Produces("application/zip", "application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadBackup(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        try
        {
            var details = await _backupService.GetBackupDetailsAsync(id, ct).ConfigureAwait(false);
            var bytes = await _backupService.DownloadBackupFileAsync(id, ct).ConfigureAwait(false);
            var fileName = Path.GetFileName(details.BackupPath);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = $"{details.BackupRunId}.zip";

            var contentType = fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                ? "application/zip"
                : "application/octet-stream";

            return File(bytes, contentType, fileName);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Backup {id} not found" });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Trigger daily billing backup for yesterday (UTC).</summary>
    [HttpPost("backup/daily")]
    [ProducesResponseType(typeof(BackupResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> TriggerDailyBackup(CancellationToken ct)
    {
        TryResolveActorUserGuid(out _, out var userId);
        var result = await _backupService
            .BackupDailyAsync(DateTime.UtcNow.Date.AddDays(-1), userId == Guid.Empty ? null : userId, ct)
            .ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Trigger weekly billing backup for the previous week.</summary>
    [HttpPost("backup/weekly")]
    [ProducesResponseType(typeof(BackupResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> TriggerWeeklyBackup(CancellationToken ct)
    {
        TryResolveActorUserGuid(out _, out var userId);
        var weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek - 7);
        var result = await _backupService
            .BackupWeeklyAsync(weekStart, userId == Guid.Empty ? null : userId, ct)
            .ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Trigger full billing backup.</summary>
    [HttpPost("backup/full")]
    [ProducesResponseType(typeof(BackupResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> TriggerFullBackup(CancellationToken ct)
    {
        TryResolveActorUserGuid(out _, out var userId);
        var result = await _backupService
            .BackupFullAsync(userId == Guid.Empty ? null : userId, ct)
            .ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Remove expired billing backups per retention policy.</summary>
    [HttpPost("backup/cleanup")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CleanupExpiredBackups(CancellationToken ct)
    {
        var deleted = await _backupService.CleanupExpiredBackupsAsync(ct).ConfigureAwait(false);
        return Ok(new { deleted });
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
