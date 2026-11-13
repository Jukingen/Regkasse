using KasseAPI_Final.Authorization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>System-critical vertical-profile assignment for one tenant.</summary>
[Authorize]
[ApiController]
[Route("api/admin/tenants/{tenantId:guid}/vertical-profile")]
[Produces("application/json")]
[HasPermission(AppPermissions.SystemCritical)]
public sealed class AdminTenantVerticalProfileController : ControllerBase
{
    private readonly IVerticalProfileService _profiles;

    public AdminTenantVerticalProfileController(IVerticalProfileService profiles)
    {
        _profiles = profiles;
    }

    [HttpGet]
    [ProducesResponseType(typeof(EffectiveVerticalProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EffectiveVerticalProfileDto>> Get(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var profile = await _profiles
            .GetForAdminTenantAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut]
    [ProducesResponseType(typeof(EffectiveVerticalProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EffectiveVerticalProfileDto>> Put(
        Guid tenantId,
        [FromBody] UpdateTenantVerticalProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var (profile, error) = await _profiles.UpdateTenantAsync(
                tenantId,
                request,
                User.GetActorUserId() ?? "unknown",
                User.GetActorRole() ?? Roles.SuperAdmin,
                cancellationToken)
            .ConfigureAwait(false);

        if (error?.Code is VerticalProfileUpdateErrorCodes.TenantNotFound
            or VerticalProfileUpdateErrorCodes.CompanySettingsMissing)
        {
            return NotFound(new { message = error.Message, code = error.Code });
        }

        if (error is not null)
            return BadRequest(new { message = error.Message, code = error.Code });

        return Ok(profile);
    }

    [HttpGet("/api/admin/tenants/{tenantId:guid}/profile-impact")]
    [ProducesResponseType(typeof(TenantProfileImpactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantProfileImpactDto>> GetImpact(
        Guid tenantId,
        [FromQuery] string profileId,
        CancellationToken cancellationToken)
    {
        var (impact, error) = await _profiles
            .GetProfileImpactAsync(tenantId, profileId, cancellationToken)
            .ConfigureAwait(false);

        if (error?.Code is VerticalProfileUpdateErrorCodes.TenantNotFound
            or VerticalProfileUpdateErrorCodes.CompanySettingsMissing)
        {
            return NotFound(new { message = error.Message, code = error.Code });
        }

        if (error is not null)
            return BadRequest(new { message = error.Message, code = error.Code });

        return Ok(impact);
    }
}
