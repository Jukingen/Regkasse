using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

public sealed record AdminStaffMemberDto(string Id, string Name, string Role);

/// <summary>
/// Tenant staff directory for catalog editors (Cashier, Waiter, Manager).
/// Not a fiscal surface.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/staff")]
[Produces("application/json")]
[HasPermission(AppPermissions.ProductView)]
public sealed class AdminStaffController : ControllerBase
{
    private static readonly string[] StaffRoles =
    [
        Roles.Cashier,
        Roles.Waiter,
        Roles.Manager,
    ];

    private readonly AppDbContext _db;

    public AdminStaffController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminStaffMemberDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminStaffMemberDto>>> List(
        CancellationToken cancellationToken)
    {
        var members = await _db.UserTenantMemberships
            .AsNoTracking()
            .Where(membership => membership.IsActive)
            .Join(
                _db.Users.AsNoTracking(),
                membership => membership.UserId,
                user => user.Id,
                (_, user) => user)
            .Where(user => user.IsActive && StaffRoles.Contains(user.Role))
            .OrderBy(user => user.LastName)
            .ThenBy(user => user.FirstName)
            .Select(user => new AdminStaffMemberDto(
                user.Id,
                ((user.FirstName ?? "") + " " + (user.LastName ?? "")).Trim(),
                user.Role))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(members);
    }
}
