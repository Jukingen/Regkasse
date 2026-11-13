using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Jahresbeleg deadline status for the signed-in tenant, or every tenant for Super Admin.</summary>
[Authorize]
[ApiController]
[Route("api/admin/jahresbeleg-reminder")]
[Produces("application/json")]
public sealed class AdminJahresbelegReminderController : ControllerBase
{
    private readonly IJahresbelegReminderService _reminders;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminJahresbelegReminderController(
        IJahresbelegReminderService reminders,
        ICurrentTenantAccessor tenantAccessor)
    {
        _reminders = reminders;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet("status")]
    [HasPermission(AppPermissions.RksvJahresbelegView)]
    [ProducesResponseType(typeof(IReadOnlyList<JahresbelegTenantStatusDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<JahresbelegTenantStatusDto>>> GetStatus(
        CancellationToken cancellationToken)
    {
        Guid? tenantId = IsSuperAdmin(User) ? null : _tenantAccessor.TenantId;
        if (tenantId == null && !IsSuperAdmin(User))
            return NotFound();

        var rows = await _reminders.GetStatusAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(rows);
    }

    private static bool IsSuperAdmin(ClaimsPrincipal user) =>
        user.IsInRole(Roles.SuperAdmin)
        || user.FindAll("role").Any(c =>
            string.Equals(c.Value, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase));
}
