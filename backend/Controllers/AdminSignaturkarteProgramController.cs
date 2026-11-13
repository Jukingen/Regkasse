using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Mai 2027 Signaturkarte program tracking (independent of certificate ExpiresAt).
/// Super Admin: platform overview; Manager: own tenant only.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/signaturkarte-program")]
[Produces("application/json")]
public sealed class AdminSignaturkarteProgramController : ControllerBase
{
    private readonly ISignaturkarteProgramService _program;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminSignaturkarteProgramController(
        ISignaturkarteProgramService program,
        ICurrentTenantAccessor tenantAccessor)
    {
        _program = program;
        _tenantAccessor = tenantAccessor;
    }

    private bool IsSuperAdmin() => User.IsInRole(Roles.SuperAdmin);

    private Guid? ManagerScopeTenantId()
    {
        if (IsSuperAdmin())
            return null;
        return _tenantAccessor.TenantId;
    }

    /// <summary>Program deadline status + totals (not certificate expiry).</summary>
    [HttpGet("status")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(SignaturkarteProgramStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SignaturkarteProgramStatusDto>> GetStatus(
        CancellationToken cancellationToken)
    {
        if (!IsSuperAdmin() && _tenantAccessor.TenantId is null)
            return NotFound();

        var status = await _program
            .GetStatusAsync(ManagerScopeTenantId(), cancellationToken)
            .ConfigureAwait(false);
        return Ok(status);
    }

    /// <summary>Device compliance list for the program.</summary>
    [HttpGet("devices")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(IReadOnlyList<SignaturkarteProgramDeviceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SignaturkarteProgramDeviceDto>>> ListDevices(
        [FromQuery] string? status = null,
        [FromQuery] Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsSuperAdmin() && _tenantAccessor.TenantId is null)
            return NotFound();

        // Managers cannot query other tenants; Super Admin may filter by tenantId.
        var filterTenant = IsSuperAdmin() ? tenantId : null;
        var devices = await _program
            .ListDevicesAsync(ManagerScopeTenantId(), status, filterTenant, cancellationToken)
            .ConfigureAwait(false);
        return Ok(devices);
    }

    /// <summary>Mark a device program-compliant (audit trail). Not related to certificate renew.</summary>
    [HttpPost("devices/{id:guid}/mark-compliant")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(SignaturkarteProgramMarkCompliantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SignaturkarteProgramMarkCompliantResponse>> MarkCompliant(
        Guid id,
        [FromBody] SignaturkarteProgramMarkCompliantRequest? body,
        CancellationToken cancellationToken)
    {
        if (!IsSuperAdmin() && _tenantAccessor.TenantId is null)
            return NotFound();

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "unknown";
        var actorRole = User.IsInRole(Roles.SuperAdmin)
            ? Roles.SuperAdmin
            : User.IsInRole(Roles.Manager)
                ? Roles.Manager
                : "User";

        var result = await _program
            .MarkCompliantAsync(
                id,
                ManagerScopeTenantId(),
                IsSuperAdmin(),
                actorId,
                actorRole,
                body?.Note,
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            if (string.Equals(result.Message, "Device not found.", StringComparison.Ordinal))
                return NotFound();
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>CSV export for Ops review (UTF-8 BOM).</summary>
    [HttpGet("export.csv")]
    [HasPermission(AppPermissions.SettingsView)]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string? status = null,
        [FromQuery] Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsSuperAdmin() && _tenantAccessor.TenantId is null)
            return NotFound();

        var filterTenant = IsSuperAdmin() ? tenantId : null;
        var (content, fileName) = await _program
            .ExportCsvAsync(ManagerScopeTenantId(), status, filterTenant, cancellationToken)
            .ConfigureAwait(false);

        return File(content, "text/csv; charset=utf-8", fileName);
    }
}
