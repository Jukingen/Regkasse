using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Tenant receipt print settings (Dankesnachricht). Canonical: <c>/api/admin/settings/receipt</c>.
/// Mandanten-Admin may edit own-tenant copy (<see cref="AppPermissions.SettingsView"/>), same as working hours.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/settings/receipt")]
[Produces("application/json")]
public sealed class AdminReceiptSettingsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AdminReceiptSettingsController> _logger;

    public AdminReceiptSettingsController(
        AppDbContext context,
        ICurrentTenantAccessor tenantAccessor,
        IAuditLogService auditLogService,
        ILogger<AdminReceiptSettingsController> logger)
    {
        _context = context;
        _tenantAccessor = tenantAccessor;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    private string ActorUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
    private string ActorRole => User.FindFirst(ClaimTypes.Role)?.Value ?? "unknown";

    [HttpGet]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(ReceiptSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReceiptSettingsDto>> GetReceiptSettings(CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var settings = await _context.CompanySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        return Ok(Map(settings));
    }

    [HttpPost]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(ReceiptSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReceiptSettingsDto>> UpdateReceiptSettings(
        [FromBody] UpdateReceiptSettingsRequest? request,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        if (request is null)
            return BadRequest(new { code = "BODY_REQUIRED", message = "Request body is required." });

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var settings = await _context.CompanySettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        var isCreate = settings == null;
        if (settings == null)
        {
            settings = CreateSettingsShell(tenantId);
            _context.CompanySettings.Add(settings);
        }

        var row = settings;
        var oldMessage = row.ThankYouMessage;
        row.ThankYouMessage = ReceiptThankYouMessage.NormalizeStored(request.ThankYouMessage);
        row.TenantId = tenantId;
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = ActorUserId;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _auditLogService.LogEntityChangeAsync(
                AuditLogActions.COMPANY_SETTINGS_UPDATE,
                AuditLogEntityTypes.COMPANY_SETTINGS,
                row.Id,
                ActorUserId,
                ActorRole,
                oldValues: isCreate ? null : new { ThankYouMessage = oldMessage },
                newValues: new { row.ThankYouMessage },
                description: "Receipt thank-you message updated.")
                .ConfigureAwait(false);
        }
        catch (Exception auditEx)
        {
            _logger.LogWarning(
                auditEx,
                "Receipt settings saved but audit log failed for tenant settings {SettingsId}",
                row.Id);
        }

        return Ok(Map(row));
    }

    private static ReceiptSettingsDto Map(CompanySettings? settings) => new()
    {
        ThankYouMessage = ReceiptThankYouMessage.NormalizeStored(settings?.ThankYouMessage),
        EffectiveThankYouMessage = ReceiptThankYouMessage.Resolve(settings),
        DefaultThankYouMessage = ReceiptThankYouMessage.Default,
        CompanyDescription = ReceiptThankYouMessage.NormalizeStored(settings?.CompanyDescription),
    };

    private static CompanySettings CreateSettingsShell(Guid tenantId) => new()
    {
        TenantId = tenantId,
        CompanyName = string.Empty,
        CompanyAddress = string.Empty,
        CompanyTaxNumber = string.Empty,
        BusinessHours = new Dictionary<string, string>(),
        WorkingHours = WorkingHoursSettings.CreateDefault(),
        AutoTagesabschluss = AutoTagesabschlussSettings.CreateDefault(),
        Currency = "EUR",
        Country = "AT",
        Language = "de-DE",
        TimeZone = "Europe/Vienna",
        DateFormat = "dd.MM.yyyy",
        TimeFormat = "HH:mm:ss",
        DecimalPlaces = 2,
        TaxCalculationMethod = "Standard",
        InvoiceNumbering = "Sequential",
        ReceiptNumbering = "Sequential",
        DefaultPaymentMethod = "Cash",
        IsActive = true,
    };
}
