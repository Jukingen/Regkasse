using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Admin: Austrian MwSt regulation catalog and rate validation helpers.
/// </summary>
[ApiController]
[Route("api/admin/tax-regulations")]
public class AdminTaxRegulationsController : ControllerBase
{
    private readonly ITaxRegulationService _taxRegulationService;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly ILogger<AdminTaxRegulationsController> _logger;

    public AdminTaxRegulationsController(
        ITaxRegulationService taxRegulationService,
        ISettingsTenantResolver settingsTenantResolver,
        ILogger<AdminTaxRegulationsController> logger)
    {
        _taxRegulationService = taxRegulationService;
        _settingsTenantResolver = settingsTenantResolver;
        _logger = logger;
    }

    [HttpGet("current")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<TaxRegulationDto>> GetCurrent(CancellationToken cancellationToken)
    {
        var regulation = await _taxRegulationService.GetCurrentRegulationAsync(cancellationToken)
            .ConfigureAwait(false);
        return Ok(ToDto(regulation));
    }

    [HttpGet("history")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<IEnumerable<TaxRegulationDto>>> GetHistory(CancellationToken cancellationToken)
    {
        var history = await _taxRegulationService.GetRegulationHistoryAsync(cancellationToken)
            .ConfigureAwait(false);
        return Ok(history.Select(ToDto));
    }

    [HttpGet("validate-rate")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<TaxRateValidationResponse>> ValidateRate(
        [FromQuery] decimal rate,
        CancellationToken cancellationToken)
    {
        var normalized = decimal.Round(rate, 2, MidpointRounding.AwayFromZero);
        var isValid = await _taxRegulationService.IsTaxRateValidAsync(normalized, cancellationToken)
            .ConfigureAwait(false);
        return Ok(new TaxRateValidationResponse { Rate = normalized, IsValid = isValid });
    }

    [HttpPost("change-impact")]
    [HasPermission(AppPermissions.ProductManage)]
    public async Task<ActionResult<TaxChangeImpactDto>> GetChangeImpact(
        [FromBody] TaxChangeImpactRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var effectiveTenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);

            // Non–Super Admin callers may only impact-assess their effective tenant.
            if (request.TenantId != effectiveTenantId)
            {
                _logger.LogWarning(
                    "Tax change impact denied: requested tenant {Requested} != effective {Effective}",
                    request.TenantId,
                    effectiveTenantId);
                return NotFound(new { message = "Tenant not found" });
            }

            var impact = await _taxRegulationService
                .GetTaxChangeImpactAsync(request.TenantId, request.OldRate, request.NewRate, cancellationToken)
                .ConfigureAwait(false);

            return Ok(new TaxChangeImpactDto
            {
                TenantId = impact.TenantId,
                OldRate = impact.OldRate,
                NewRate = impact.NewRate,
                AffectedProductCount = impact.AffectedProductCount,
                AffectedCatalogValue = impact.AffectedCatalogValue,
                EstimatedVatDelta = impact.EstimatedVatDelta,
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing tax change impact");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    private static TaxRegulationDto ToDto(TaxRegulation regulation) => new()
    {
        EffectiveDate = regulation.EffectiveDate,
        StandardRate = regulation.StandardRate,
        ReducedRate = regulation.ReducedRate,
        ReducedNewRate = regulation.ReducedNewRate,
        MiddleRate = regulation.MiddleRate,
        ZeroRate = regulation.ZeroRate,
        IsActive = regulation.IsActive,
        Description = regulation.Description,
        AllowedRates = TaxRegulationService.GetDistinctAllowedRates(regulation)
            .OrderBy(r => r)
            .ToArray(),
    };
}
