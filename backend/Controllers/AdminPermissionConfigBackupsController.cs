using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/permission-config-backups")]
[Produces("application/json")]
public sealed class AdminPermissionConfigBackupsController : ControllerBase
{
    private readonly IPermissionConfigBackupService _backups;

    public AdminPermissionConfigBackupsController(IPermissionConfigBackupService backups)
    {
        _backups = backups;
    }

    [HttpGet]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionConfigBackupListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PermissionConfigBackupListItemDto>>> List(
        CancellationToken cancellationToken)
    {
        return Ok(await _backups.ListAsync(cancellationToken));
    }

    [HttpPost]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(PermissionConfigBackupListItemDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PermissionConfigBackupListItemDto>> Create(
        [FromBody] CreatePermissionConfigBackupRequest? body,
        CancellationToken cancellationToken)
    {
        var created = await _backups.CreateAsync(body, User.GetActorUserId(), cancellationToken: cancellationToken);
        return Ok(created);
    }

    [HttpGet("{id:guid}/preview-restore")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(PermissionConfigRestorePreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PermissionConfigRestorePreviewDto>> PreviewRestore(
        Guid id,
        CancellationToken cancellationToken)
    {
        var preview = await _backups.PreviewRestoreAsync(id, cancellationToken);
        return preview is null ? NotFound() : Ok(preview);
    }

    [HttpPost("{id:guid}/restore")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        var (succeeded, code, error) = await _backups.RestoreAsync(id, User.GetActorUserId(), cancellationToken);
        if (!succeeded)
        {
            return code == PermissionConfigBackupService.NotFoundCode
                ? NotFound(new { code, error })
                : BadRequest(new { code, error });
        }

        return NoContent();
    }

    [HttpGet("settings")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(PermissionConfigBackupSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PermissionConfigBackupSettingsDto>> GetSettings(
        CancellationToken cancellationToken)
    {
        return Ok(await _backups.GetSettingsAsync(cancellationToken));
    }

    [HttpPut("settings")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(PermissionConfigBackupSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PermissionConfigBackupSettingsDto>> SetSettings(
        [FromBody] PermissionConfigBackupSettingsDto body,
        CancellationToken cancellationToken)
    {
        return Ok(await _backups.SetSettingsAsync(body, cancellationToken));
    }
}
