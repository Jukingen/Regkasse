using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Website;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Sites.Controllers;

/// <summary>
/// Anonymous working-hours status for tenant websites / PWAs / native apps.
/// Applies only to customer-facing online ordering — never gates POS or FA.
/// Special days come from <c>CompanySettings.WorkingHours.SpecialDays</c> JSON (no separate table).
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/sites/{tenantSlug}/status")]
[Produces("application/json")]
public sealed class WebsiteStatusController : ControllerBase
{
    private readonly IPublicTenantCatalogService _catalog;

    public WebsiteStatusController(IPublicTenantCatalogService catalog)
    {
        _catalog = catalog;
    }

    /// <summary>
    /// GET open / can-order / isSpecial status for a tenant website (slug-scoped, no JWT).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(WebsiteStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WebsiteStatusDto>> GetStatus(
        [FromRoute] string tenantSlug,
        CancellationToken ct = default)
    {
        var status = await _catalog.GetWebsiteStatusAsync(tenantSlug, ct);
        if (status is null)
            return NotFound();

        return Ok(status);
    }

    /// <summary>
    /// GET today's special-day override (holiday / custom hours) from WorkingHours JSON.
    /// </summary>
    [HttpGet("special")]
    [ProducesResponseType(typeof(WebsiteSpecialDayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WebsiteSpecialDayDto>> GetSpecialDay(
        [FromRoute] string tenantSlug,
        CancellationToken ct = default)
    {
        var special = await _catalog.GetWebsiteSpecialDayAsync(tenantSlug, ct);
        if (special is null)
            return NotFound();

        return Ok(special);
    }
}
