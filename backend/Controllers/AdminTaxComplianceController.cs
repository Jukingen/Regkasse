using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Admin: Austrian MwSt product catalog compliance check.</summary>
[ApiController]
[Route("api/admin/tax-compliance")]
public class AdminTaxComplianceController : ControllerBase
{
    private readonly ITaxComplianceChecker _complianceChecker;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly ILogger<AdminTaxComplianceController> _logger;

    public AdminTaxComplianceController(
        ITaxComplianceChecker complianceChecker,
        ISettingsTenantResolver settingsTenantResolver,
        ILogger<AdminTaxComplianceController> logger)
    {
        _complianceChecker = complianceChecker;
        _settingsTenantResolver = settingsTenantResolver;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(ComplianceReport), StatusCodes.Status200OK)]
    public async Task<ActionResult<ComplianceReport>> Check(CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            var report = await _complianceChecker
                .CheckComplianceAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(report);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running tax compliance check");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
