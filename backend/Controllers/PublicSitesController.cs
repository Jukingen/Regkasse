using KasseAPI_Final.Sites;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Dynamic tenant website HTML (live menu). Complements static /media/sites snapshots
/// and Next.js frontend-sites — same catalog source.
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/public/sites")]
public sealed class PublicSitesController : ControllerBase
{
    private readonly ITenantWebsiteService _websites;

    public PublicSitesController(ITenantWebsiteService websites)
    {
        _websites = websites;
    }

    /// <summary>GET live HTML for tenant slug. Query: template=modern|classic|minimal.</summary>
    [HttpGet("{slug}")]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWebsite(
        [FromRoute] string slug,
        [FromQuery] string? template = null,
        CancellationToken ct = default)
    {
        var html = await _websites.GetWebsiteHtmlAsync(slug, template, ct);
        if (html is null)
            return NotFound();

        return Content(html, "text/html; charset=utf-8");
    }
}
