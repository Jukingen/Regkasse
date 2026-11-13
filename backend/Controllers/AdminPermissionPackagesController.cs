using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/permission-packages")]
[Produces("application/json")]
public sealed class AdminPermissionPackagesController : ControllerBase
{
    private readonly IPermissionPackageService _packages;
    private readonly IRolePermissionSimulateService _simulate;
    private readonly IPermissionConfigBackupService _backups;

    public AdminPermissionPackagesController(
        IPermissionPackageService packages,
        IRolePermissionSimulateService simulate,
        IPermissionConfigBackupService backups)
    {
        _packages = packages;
        _simulate = simulate;
        _backups = backups;
    }

    [HttpGet]
    [HasPermission(AppPermissions.RoleView)]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionPackageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PermissionPackageDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _packages.ListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.RoleView)]
    [ProducesResponseType(typeof(PermissionPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PermissionPackageDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var row = await _packages.GetAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(PermissionPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PermissionPackageDto>> Create(
        [FromBody] UpsertPermissionPackageRequest body,
        CancellationToken cancellationToken)
    {
        await _backups.TryAutoBackupBeforeChangeAsync(User.GetActorUserId(), cancellationToken);
        var created = await _packages.CreateAsync(body, User.GetActorUserId(), cancellationToken);
        return created is null ? BadRequest(new { code = PermissionPackageService.InvalidPermissionsCode }) : Ok(created);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(PermissionPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PermissionPackageDto>> Update(
        Guid id,
        [FromBody] UpsertPermissionPackageRequest body,
        CancellationToken cancellationToken)
    {
        await _backups.TryAutoBackupBeforeChangeAsync(User.GetActorUserId(), cancellationToken);
        var updated = await _packages.UpdateAsync(id, body, cancellationToken);
        if (updated is null)
        {
            var existing = await _packages.GetAsync(id, cancellationToken);
            return existing is null ? NotFound() : BadRequest(new { code = PermissionPackageService.SystemImmutableCode });
        }

        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _backups.TryAutoBackupBeforeChangeAsync(User.GetActorUserId(), cancellationToken);
        var (succeeded, code, error) = await _packages.DeleteAsync(id, cancellationToken);
        if (!succeeded)
        {
            return code == PermissionPackageService.NotFoundCode
                ? NotFound(new { code, error })
                : BadRequest(new { code, error });
        }

        return NoContent();
    }

    [HttpGet("roles/{roleName}/packages")]
    [HasPermission(AppPermissions.RoleView)]
    [ProducesResponseType(typeof(IReadOnlyList<RoleAssignedPackageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleAssignedPackageDto>>> ListForRole(
        string roleName,
        CancellationToken cancellationToken)
    {
        return Ok(await _packages.ListAssignedPackagesForRoleAsync(roleName, cancellationToken));
    }

    [HttpPost("roles/{roleName}/packages/{packageId:guid}")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddToRole(
        string roleName,
        Guid packageId,
        CancellationToken cancellationToken)
    {
        await _backups.TryAutoBackupBeforeChangeAsync(User.GetActorUserId(), cancellationToken);
        var (succeeded, code, error) = await _packages.AddPackageToRoleAsync(
            roleName,
            packageId,
            User.GetActorUserId(),
            cancellationToken);
        return succeeded ? NoContent() : BadRequest(new { code, error });
    }

    [HttpDelete("roles/{roleName}/packages/{packageId:guid}")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveFromRole(
        string roleName,
        Guid packageId,
        CancellationToken cancellationToken)
    {
        await _backups.TryAutoBackupBeforeChangeAsync(User.GetActorUserId(), cancellationToken);
        var (succeeded, code, error) = await _packages.RemovePackageFromRoleAsync(
            roleName,
            packageId,
            cancellationToken);
        return succeeded ? NoContent() : BadRequest(new { code, error });
    }

    [HttpPost("roles/{roleName}/permissions/simulate")]
    [HasPermission(AppPermissions.RoleView)]
    [ProducesResponseType(typeof(RolePermissionSimulateResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RolePermissionSimulateResultDto>> Simulate(
        string roleName,
        [FromBody] RolePermissionSimulateRequest? body,
        CancellationToken cancellationToken)
    {
        body ??= new RolePermissionSimulateRequest();
        var result = await _simulate.SimulateAsync(
            roleName,
            body.ProposedPermissions,
            body.Page,
            body.PageSize,
            cancellationToken);
        return Ok(result);
    }
}
