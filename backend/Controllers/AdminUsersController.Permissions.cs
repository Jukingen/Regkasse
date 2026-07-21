using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

public partial class AdminUsersController
{
    /// <summary>
    /// Effective permission strings for a user (roles + overrides). Same source as JWT / GET /api/Auth/me.
    /// Self-service: any authenticated user may read their own list. SuperAdmin may read any user (tenant-scoped 404).
    /// </summary>
    [HttpGet("{id}/permissions")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<string>>> GetUserPermissions(
        string id,
        [FromServices] IEffectivePermissionResolver effectivePermissionResolver,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { message = "User id is required.", code = "VALIDATION_ERROR" });

        var actorId = ActorId;
        if (string.IsNullOrEmpty(actorId))
            return Unauthorized(new { message = "User not authenticated", code = "UNAUTHORIZED" });

        var viewingSelf = string.Equals(actorId, id, StringComparison.Ordinal);
        if (!viewingSelf && !IsActorSuperAdmin())
            return Forbid();

        var user = await _userManager.FindByIdAsync(id).ConfigureAwait(false);
        if (user == null)
            return NotFound(new { message = "User not found" });

        if (!viewingSelf)
        {
            if (!await IsUserAccessibleInAmbientTenantAsync(id, cancellationToken).ConfigureAwait(false))
                return NotFound(new { message = "User not found" });
        }
        else if (_tenantAccessor.TenantId is Guid tenantId
                 && !await ValidateUserInTenantAsync(id, tenantId, cancellationToken).ConfigureAwait(false))
        {
            return NotFound(new { message = "User not found" });
        }

        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        var permissions = await effectivePermissionResolver
            .GetEffectivePermissionsAsync(id, roles, _tenantAccessor.TenantId, cancellationToken)
            .ConfigureAwait(false);

        return Ok(permissions.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList());
    }
}
