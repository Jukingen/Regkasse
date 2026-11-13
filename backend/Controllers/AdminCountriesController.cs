using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin country-profile catalog (tenant-selectable seeds only).</summary>
[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/admin/countries")]
[Produces("application/json")]
public sealed class AdminCountriesController : ControllerBase
{
    private readonly ICountryProfileRegistry _countries;

    public AdminCountriesController(ICountryProfileRegistry countries)
    {
        _countries = countries;
    }

    /// <summary>Tenant-selectable country profiles (excludes registry-only <c>EU_DEFAULT</c>).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CountryProfileSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<IReadOnlyList<CountryProfileSummaryDto>> List()
    {
        var items = _countries.TenantSelectable
            .Select(CountryProfileSummaryDto.From)
            .ToList();
        return Ok(items);
    }
}
