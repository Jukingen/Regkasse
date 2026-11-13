using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin EF migration status (read-only).</summary>
[Authorize]
[ApiController]
[Route("api/admin/database/migrations")]
[Produces("application/json")]
public sealed class AdminDatabaseMigrationsController : ControllerBase
{
    private readonly IMigrationStatusService _migrations;

    public AdminDatabaseMigrationsController(IMigrationStatusService migrations)
    {
        _migrations = migrations;
    }

    /// <summary>Pending + recent applied migrations for the connected database.</summary>
    [HttpGet]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(AdminMigrationStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminMigrationStatusDto>> Get(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _migrations.GetAdminStatusAsync(take, cancellationToken).ConfigureAwait(false));
    }
}
