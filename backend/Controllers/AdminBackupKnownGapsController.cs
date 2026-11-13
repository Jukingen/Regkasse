using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Backup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin acknowledgement of Backup/DR known gaps for one mandant.
/// Acceptance does not disable the checks and does not move restore-proof timestamps.
/// </summary>
[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/admin/backup/known-gaps")]
[Produces("application/json")]
public sealed class AdminBackupKnownGapsController : ControllerBase
{
    private readonly IBackupKnownGapAcceptanceService _acceptance;
    private readonly IBackupKnownGapStatusService _status;
    private readonly IAdminTenantService _tenants;

    public AdminBackupKnownGapsController(
        IBackupKnownGapAcceptanceService acceptance,
        IBackupKnownGapStatusService status,
        IAdminTenantService tenants)
    {
        _acceptance = acceptance;
        _status = status;
        _tenants = tenants;
    }

    /// <summary>Catalog, the tenant acceptance record, and the current card (red / amber / green).</summary>
    [HttpGet]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(BackupKnownGapViewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BackupKnownGapViewDto>> Get(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId is null || tenantId == Guid.Empty)
            return BadRequest(new { message = "Tenant id is required." });

        if (await _tenants.GetByIdAsync(tenantId.Value, cancellationToken).ConfigureAwait(false) is null)
            return NotFound();

        return Ok(await _status.GetViewAsync(tenantId.Value, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Replaces accepted catalog ids. <c>acceptedAtUtc</c> is stamped on the server.
    /// Unknown ids return HTTP 400 <c>BACKUP_KNOWN_GAP_UNKNOWN</c>.
    /// </summary>
    [HttpPut]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(BackupKnownGapViewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BackupKnownGapViewDto>> Save(
        [FromBody] SaveBackupKnownGapsRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.TenantId == Guid.Empty)
            return BadRequest(new { message = "Tenant id is required." });
        if (request.AcceptedGapIds is null)
            return BadRequest(new { message = "Accepted gap ids are required.", code = BackupKnownGapUnknownException.ErrorCode, invalidGapIds = Array.Empty<string>() });
        if (string.IsNullOrWhiteSpace(request.AcceptedBy))
            return BadRequest(new { message = "Accepted-by is required." });

        if (await _tenants.GetByIdAsync(request.TenantId, cancellationToken).ConfigureAwait(false) is null)
            return NotFound();

        try
        {
            await _acceptance.SaveAsync(
                request.TenantId,
                request.AcceptedGapIds,
                request.AcceptedBy,
                DateTimeOffset.UtcNow,
                cancellationToken).ConfigureAwait(false);
        }
        catch (BackupKnownGapUnknownException ex)
        {
            return BadRequest(new
            {
                code = BackupKnownGapUnknownException.ErrorCode,
                message = ex.Message,
                invalidGapIds = ex.InvalidGapIds,
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return Ok(await _status.GetViewAsync(request.TenantId, cancellationToken).ConfigureAwait(false));
    }
}

public sealed class SaveBackupKnownGapsRequest
{
    public Guid TenantId { get; set; }

    public IReadOnlyList<string>? AcceptedGapIds { get; set; }

    public string? AcceptedBy { get; set; }
}
