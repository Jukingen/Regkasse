using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin view of the CH MWST percents in force for one mandant.</summary>
[Authorize]
[ApiController]
[Route("api/admin/mwst")]
[Produces("application/json")]
public sealed class AdminMwstRatesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IChMwstEffectiveRates _rates;

    public AdminMwstRatesController(AppDbContext db, IChMwstEffectiveRates rates)
    {
        _db = db;
        _rates = rates;
    }

    /// <summary>
    /// Effective Swiss MWST rates for <paramref name="tenantId"/>.
    /// Missing tenant → 404. A non-CH mandant returns the code seed with <c>applies: false</c>.
    /// </summary>
    [HttpGet("tenants/{tenantId:guid}/rates")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(ChMwstEffectiveRatesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChMwstEffectiveRatesDto>> GetRates(Guid tenantId, CancellationToken cancellationToken)
    {
        var exists = await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            return NotFound();

        var country = await _db.CompanySettings.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.Country)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var applies = string.Equals(country?.Trim(), CountryProfileCodes.Switzerland, StringComparison.OrdinalIgnoreCase);
        var resolution = _rates.Resolve(tenantId, applyOverride: applies);
        var rates = resolution.Rates
            .Select(row => new ChMwstRateDto(row.Code, row.Rate, row.Label, row.EffectiveFrom))
            .ToArray();

        return Ok(new ChMwstEffectiveRatesDto(
            tenantId,
            country,
            applies,
            resolution.Source,
            rates));
    }
}

public sealed record ChMwstEffectiveRatesDto(
    Guid TenantId,
    string? Country,
    bool Applies,
    string Source,
    IReadOnlyList<ChMwstRateDto> Rates);

public sealed record ChMwstRateDto(
    string Code,
    decimal Rate,
    string Label,
    DateOnly EffectiveFrom);
