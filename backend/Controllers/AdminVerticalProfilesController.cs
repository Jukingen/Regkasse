using KasseAPI_Final.Authorization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>System-critical catalog of POS vertical profiles, including database overrides.</summary>
[Authorize]
[ApiController]
[Route("api/admin/vertical-profiles")]
[Produces("application/json")]
[HasPermission(AppPermissions.SystemCritical)]
public sealed class AdminVerticalProfilesController : ControllerBase
{
    private readonly IVerticalProfileService _profiles;
    private readonly IVerticalProfileCatalogService _catalog;

    public AdminVerticalProfilesController(
        IVerticalProfileService profiles,
        IVerticalProfileCatalogService catalog)
    {
        _profiles = profiles;
        _catalog = catalog;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<VerticalProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VerticalProfileDto>>> List(
        CancellationToken cancellationToken)
    {
        var profiles = await _profiles.ListActiveAsync(cancellationToken).ConfigureAwait(false);
        return Ok(profiles);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(VerticalProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VerticalProfileDto>> Get(
        string id,
        CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetActiveAsync(id, cancellationToken).ConfigureAwait(false);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPost]
    [ProducesResponseType(typeof(VerticalProfileMutationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VerticalProfileMutationResult>> Create(
        [FromBody] UpsertVerticalProfileRequest request,
        CancellationToken cancellationToken)
    {
        var (result, error) = await _catalog.CreateAsync(
                request,
                ActorId(),
                ActorRole(),
                cancellationToken)
            .ConfigureAwait(false);
        return error is null ? Ok(result) : CatalogError(error);
    }

    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(VerticalProfileMutationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VerticalProfileMutationResult>> Patch(
        string id,
        [FromBody] UpsertVerticalProfileRequest request,
        CancellationToken cancellationToken)
    {
        var (result, error) = await _catalog.UpdateAsync(
                id,
                request,
                ActorId(),
                ActorRole(),
                cancellationToken)
            .ConfigureAwait(false);
        return error is null ? Ok(result) : CatalogError(error);
    }

    [HttpPut("{id}/features")]
    [ProducesResponseType(typeof(VerticalProfileMutationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VerticalProfileMutationResult>> PutFeatures(
        string id,
        [FromBody] UpdateVerticalProfileFeaturesRequest request,
        CancellationToken cancellationToken)
    {
        var (result, error) = await _catalog.UpdateFeaturesAsync(
                id,
                request,
                ActorId(),
                ActorRole(),
                cancellationToken)
            .ConfigureAwait(false);
        return error is null ? Ok(result) : CatalogError(error);
    }

    [HttpPost("{id}/clone")]
    [ProducesResponseType(typeof(VerticalProfileMutationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VerticalProfileMutationResult>> Clone(
        string id,
        [FromBody] CloneVerticalProfileRequest request,
        CancellationToken cancellationToken)
    {
        var (result, error) = await _catalog.CloneAsync(
                id,
                request,
                ActorId(),
                ActorRole(),
                cancellationToken)
            .ConfigureAwait(false);
        return error is null ? Ok(result) : CatalogError(error);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var error = await _catalog.DeleteAsync(id, ActorId(), ActorRole(), cancellationToken)
            .ConfigureAwait(false);
        return error is null ? NoContent() : CatalogError(error);
    }

    [HttpGet("{id}/tenants")]
    [ProducesResponseType(typeof(IReadOnlyList<VerticalProfileTenantSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<VerticalProfileTenantSummaryDto>>> Tenants(
        string id,
        CancellationToken cancellationToken)
    {
        var tenants = await _catalog.ListTenantsAsync(id, cancellationToken).ConfigureAwait(false);
        return tenants is null ? NotFound() : Ok(tenants);
    }

    [HttpGet("/api/admin/tenants/by-profile")]
    [ProducesResponseType(typeof(IReadOnlyList<VerticalProfileTenantGroupDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VerticalProfileTenantGroupDto>>> TenantsByProfile(
        CancellationToken cancellationToken)
    {
        var groups = await _catalog.GroupTenantsByProfileAsync(cancellationToken).ConfigureAwait(false);
        return Ok(groups);
    }

    private string ActorId() => User.GetActorUserId() ?? "unknown";

    private string ActorRole() => User.GetActorRole() ?? Roles.SuperAdmin;

    private ObjectResult CatalogError(VerticalProfileCatalogError error)
    {
        var body = new
        {
            code = error.Code,
            message = error.Message,
            tenants = error.Tenants,
        };
        return error.StatusCode switch
        {
            StatusCodes.Status404NotFound => NotFound(body),
            StatusCodes.Status409Conflict => Conflict(body),
            _ => BadRequest(body),
        };
    }
}
