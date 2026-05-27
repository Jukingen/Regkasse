using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Services.Dev;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Development-only maintenance endpoints (no-op / hidden outside Development).
/// </summary>
[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/dev/cleanup")]
[Produces("application/json")]
public sealed class DevCleanupController : ControllerBase
{
    private readonly IHostEnvironment _environment;
    private readonly AppDbContext _db;
    private readonly ILogger<DevCleanupController> _logger;

    public DevCleanupController(
        IHostEnvironment environment,
        AppDbContext db,
        ILogger<DevCleanupController> logger)
    {
        _environment = environment;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Removes test users (bar/cafe/test email patterns) and their tenant memberships.
    /// </summary>
    [HttpPost("orphaned-users")]
    [ProducesResponseType(typeof(DevOrphanedUserCleanupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DevOrphanedUserCleanupResponse>> CleanupOrphanedUsers(
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        var users = await DevOrphanedUserCleanup
            .WhereOrphanedTestUserEmail(_db.Users)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (users.Count == 0)
        {
            return Ok(new DevOrphanedUserCleanupResponse(
                "Cleanup completed",
                DeletedMemberships: 0,
                DeletedUsers: 0));
        }

        var userIds = users.Select(u => u.Id).ToList();

        var memberships = await _db.UserTenantMemberships
            .IgnoreQueryFilters()
            .Where(m => userIds.Contains(m.UserId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (memberships.Count > 0)
            _db.UserTenantMemberships.RemoveRange(memberships);

        _db.Users.RemoveRange(users);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Dev orphaned-user cleanup removed {MembershipCount} memberships and {UserCount} users",
            memberships.Count,
            users.Count);

        return Ok(new DevOrphanedUserCleanupResponse(
            "Cleanup completed",
            memberships.Count,
            users.Count));
    }
}

public sealed record DevOrphanedUserCleanupResponse(
    string Message,
    int DeletedMemberships,
    int DeletedUsers);
