using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

public sealed record PosStaffMemberDto(string Id, string Name, string Role);

/// <summary>
/// Tenant staff directory for POS appointment booking (Cashier, Waiter, Manager).
/// Not a fiscal surface.
/// </summary>
[Authorize]
[ApiController]
[Route("api/pos/staff")]
[Produces("application/json")]
[HasPermission(AppPermissions.CartView)]
public sealed class PosStaffController : ControllerBase
{
    private static readonly string[] StaffRoles =
    [
        Roles.Cashier,
        Roles.Waiter,
        Roles.Manager,
    ];

    private readonly AppDbContext _db;

    public PosStaffController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PosStaffMemberDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PosStaffMemberDto>>> List(
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
            .Select(user => new PosStaffMemberDto(
                user.Id,
                ((user.FirstName ?? "") + " " + (user.LastName ?? "")).Trim(),
                user.Role))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(members);
    }
}
