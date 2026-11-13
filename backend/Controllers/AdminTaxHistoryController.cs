using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Admin: product MwSt rate change journal.</summary>
[ApiController]
[Route("api/admin/tax-history")]
public class AdminTaxHistoryController : ControllerBase
{
    private readonly ITaxHistoryService _taxHistoryService;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly ILogger<AdminTaxHistoryController> _logger;

    public AdminTaxHistoryController(
        ITaxHistoryService taxHistoryService,
        ISettingsTenantResolver settingsTenantResolver,
        ILogger<AdminTaxHistoryController> logger)
    {
        _taxHistoryService = taxHistoryService;
        _settingsTenantResolver = settingsTenantResolver;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<IEnumerable<TaxHistoryItemDto>>> GetHistory(
        [FromQuery] Guid? productId = null,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            var items = await _taxHistoryService
                .GetHistoryAsync(tenantId, productId, take, cancellationToken)
                .ConfigureAwait(false);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing tax history");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
