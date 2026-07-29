using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin feature-flag management (config defaults + tenant_settings overrides).</summary>
[Authorize]
[ApiController]
[Route("api/admin/feature-flags")]
[Produces("application/json")]
public sealed class AdminFeatureFlagsController : ControllerBase
{
    private readonly IFeatureFlagService _flags;

    public AdminFeatureFlagsController(IFeatureFlagService flags)
    {
        _flags = flags;
    }

    /// <summary>List known flags with effective state (optional tenant scope).</summary>
    [HttpGet]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<FeatureFlagStatusDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FeatureFlagStatusDto>>> List(
        [FromQuery] string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _flags.GetStatusesAsync(tenantId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Set or clear a flag override (global or per tenant).</summary>
    [HttpPut]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(FeatureFlagStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FeatureFlagStatusDto>> Set(
        [FromBody] SetFeatureFlagRequest? body,
        CancellationToken cancellationToken = default)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { message = "Name is required." });

        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "unknown";
        try
        {
            if (body.ClearOverride)
            {
                await _flags.ClearOverrideAsync(body.Name, body.TenantId, actor, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await _flags.SetEnabledAsync(body.Name, body.Enabled, body.TenantId, actor, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var statuses = await _flags.GetStatusesAsync(body.TenantId, cancellationToken).ConfigureAwait(false);
        var name = FeatureFlagNames.Normalize(body.Name);
        var match = statuses.FirstOrDefault(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        return Ok(match ?? statuses.First());
    }

    /// <summary>Quick check: is a flag enabled for an optional tenant?</summary>
    [HttpGet("{name}/enabled")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult<object> IsEnabled(string name, [FromQuery] string? tenantId = null)
    {
        var enabled = _flags.IsEnabled(name, tenantId);
        return Ok(new
        {
            name = FeatureFlagNames.Normalize(name),
            enabled,
            tenantId,
        });
    }
}
