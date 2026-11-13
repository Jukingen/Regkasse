using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Read-only what-if impact reports for critical / sensitive tenant changes.
/// Does not require critical-action approval (preview only; no mutations).
/// </summary>
[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/admin/impact-simulation")]
[Produces("application/json")]
public sealed class AdminImpactSimulationController : ControllerBase
{
    private readonly IImpactSimulationService _simulation;

    public AdminImpactSimulationController(IImpactSimulationService simulation)
    {
        _simulation = simulation;
    }

    /// <summary>Simulate the impact of a proposed tax rate, currency, or product price change.</summary>
    [HttpPost("simulate")]
    [ProducesResponseType(typeof(ImpactReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImpactReportDto>> Simulate(
        [FromBody] ImpactSimulationRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (body is null || body.TenantId == Guid.Empty)
            return BadRequest(new { code = "INVALID_BODY", message = "tenantId is required." });

        try
        {
            ImpactReport report = body.ChangeType switch
            {
                ChangeType.TaxRate => await SimulateTaxAsync(body, cancellationToken).ConfigureAwait(false),
                ChangeType.Currency => await SimulateCurrencyAsync(body, cancellationToken).ConfigureAwait(false),
                ChangeType.ProductPrice => await SimulatePricesAsync(body, cancellationToken).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(body.ChangeType), "Unsupported change type."),
            };

            return Ok(report.ToDto());
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "INVALID_SIMULATION", message = ex.Message });
        }
    }

    private Task<ImpactReport> SimulateTaxAsync(ImpactSimulationRequestDto body, CancellationToken cancellationToken)
    {
        if (body.NewTaxRate is null)
            throw new ArgumentException("newTaxRate is required for TaxRate simulation.");

        return _simulation.SimulateTaxRateChangeAsync(
            body.TenantId,
            body.NewTaxRate.Value,
            body.CurrentTaxRate,
            cancellationToken);
    }

    private Task<ImpactReport> SimulateCurrencyAsync(ImpactSimulationRequestDto body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.NewCurrency))
            throw new ArgumentException("newCurrency is required for Currency simulation.");

        return _simulation.SimulateCurrencyChangeAsync(body.TenantId, body.NewCurrency, cancellationToken);
    }

    private Task<ImpactReport> SimulatePricesAsync(ImpactSimulationRequestDto body, CancellationToken cancellationToken)
    {
        if (body.ProductPriceUpdates is null || body.ProductPriceUpdates.Count == 0)
            throw new ArgumentException("productPriceUpdates is required for ProductPrice simulation.");

        var updates = body.ProductPriceUpdates
            .Select(u => new ProductPriceUpdate { ProductId = u.ProductId, NewPrice = u.NewPrice })
            .ToList();

        return _simulation.SimulatePriceChangeAsync(body.TenantId, updates, cancellationToken);
    }
}
