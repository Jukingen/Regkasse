using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Website;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Anonymous tenant profile + live menu for shared website / PWA platform.
/// Same catalog source as AppGenerator snapshots — no JWT, slug-scoped only.
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/public/tenants")]
[Produces("application/json")]
public sealed class PublicTenantsController : ControllerBase
{
    private readonly IPublicTenantCatalogService _catalog;

    public PublicTenantsController(IPublicTenantCatalogService catalog)
    {
        _catalog = catalog;
    }

    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(PublicTenantProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicTenantProfileDto>> GetProfile(
        [FromRoute] string slug,
        CancellationToken ct = default)
    {
        var profile = await _catalog.GetProfileAsync(slug, ct);
        if (profile is null)
            return NotFound();
        return Ok(profile);
    }

    [HttpGet("{slug}/menu")]
    [ProducesResponseType(typeof(PublicTenantMenuDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicTenantMenuDto>> GetMenu(
        [FromRoute] string slug,
        CancellationToken ct = default)
    {
        var menu = await _catalog.GetMenuAsync(slug, ct);
        if (menu is null)
            return NotFound();
        return Ok(menu);
    }
}
