using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// PITR planning, WAL inventory, backup chain, pre-restore checks, and isolated dry-run.
/// Production restore is never started from this controller.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/backup")]
[Produces("application/json")]
public sealed class AdminBackupPitrController : ControllerBase
{
    private readonly IPitrService _pitr;
    private readonly IWalArchiveService _wal;
    private readonly IBackupChainService _chain;
    private readonly IIncrementalBackupService _incremental;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminBackupPitrController(
        IPitrService pitr,
        IWalArchiveService wal,
        IBackupChainService chain,
        IIncrementalBackupService incremental,
        ICurrentTenantAccessor tenantAccessor)
    {
        _pitr = pitr;
        _wal = wal;
        _chain = chain;
        _incremental = incremental;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet("pitr/wal")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(WalArchiveStatusDto), StatusCodes.Status200OK)]
    public ActionResult<WalArchiveStatusDto> GetWalStatus() => Ok(_wal.GetStatus());

    [HttpGet("pitr/chain")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(BackupChainResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BackupChainResponseDto>> GetChain(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var scope = BuildScope();
        if (!scope.IsSuperAdmin && !scope.CallerTenantId.HasValue)
            return BadRequest(new { code = "TENANT_REQUIRED", message = "Tenant context is required." });

        var filter = scope.IsSuperAdmin ? tenantId ?? scope.CallerTenantId : scope.CallerTenantId;
        return Ok(await _chain.GetChainAsync(filter, scope, cancellationToken));
    }

    [HttpPost("pitr/pre-restore-validate")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(PitrPreRestoreValidationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PitrPreRestoreValidationDto>> PreRestoreValidate(
        [FromBody] ValidatePitrRestorePointRequestDto? body,
        CancellationToken cancellationToken)
    {
        var tenantGuard = ResolveTenantFilter(out var tenantId);
        if (tenantGuard != null)
            return tenantGuard;
        if (body == null)
            return BadRequest(new { code = "INVALID_BODY", message = "Request body is required." });

        return Ok(await _pitr.ValidatePreRestoreAsync(tenantId, body.TargetTimeUtc, cancellationToken));
    }

    [HttpPost("pitr/dry-run")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(PitrDryRunResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PitrDryRunResponseDto>> DryRun(
        [FromBody] PitrDryRunRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRole(Roles.SuperAdmin))
            return Forbid();
        if (body == null)
            return BadRequest(new { code = "INVALID_BODY", message = "Request body is required." });

        var tenantId = body.TenantId ?? _tenantAccessor.TenantId;
        var actor = User.GetActorUserId() ?? "unknown";
        return Ok(await _pitr.RequestDryRunAsync(tenantId, body.TargetTimeUtc, actor, cancellationToken));
    }

    [HttpPost("incremental")]
    [HasPermission(AppPermissions.BackupManage)]
    [ProducesResponseType(typeof(BackupResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BackupResult>> TriggerIncremental(
        [FromBody] IncrementalBackupTriggerRequestDto? body,
        CancellationToken cancellationToken)
    {
        var tenantId = body?.TenantId ?? _tenantAccessor.TenantId;
        if (!tenantId.HasValue)
            return BadRequest(new { code = "TENANT_REQUIRED", message = "Tenant id is required for incremental backup." });

        if (!User.IsInRole(Roles.SuperAdmin)
            && _tenantAccessor.TenantId is Guid ambient
            && ambient != tenantId.Value)
        {
            return NotFound();
        }

        if (!Guid.TryParse(User.GetActorUserId(), out var userId))
            userId = Guid.Empty;

        var since = body?.SinceUtc ?? DateTime.UtcNow.AddDays(-1);
        var result = await _incremental.CreateIncrementalBackupAsync(tenantId.Value, userId, since, cancellationToken);
        if (!result.Succeeded)
            return BadRequest(new { code = result.Code, message = result.Error });
        return Ok(result);
    }

    [HttpPost("incremental/restore")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(IncrementalRestoreResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IncrementalRestoreResultDto>> RestoreFromIncremental(
        [FromBody] PitrDryRunRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRole(Roles.SuperAdmin))
            return Forbid();
        if (body == null)
            return BadRequest(new { code = "INVALID_BODY", message = "Request body is required." });

        var tenantId = body.TenantId ?? _tenantAccessor.TenantId;
        if (!tenantId.HasValue)
            return BadRequest(new { code = "TENANT_REQUIRED", message = "Tenant id is required." });

        var actor = User.GetActorUserId() ?? "unknown";
        return Ok(await _incremental.RestoreFromIncrementalAsync(
            tenantId.Value,
            body.TargetTimeUtc,
            actor,
            cancellationToken));
    }

    private BackupRunAccessScope BuildScope() =>
        new(User.IsInRole(Roles.SuperAdmin), _tenantAccessor.TenantId, User.GetActorUserId());

    private ActionResult? ResolveTenantFilter(out Guid? tenantId)
    {
        tenantId = _tenantAccessor.TenantId;
        if (tenantId.HasValue || User.IsInRole(Roles.SuperAdmin))
            return null;
        return BadRequest(new { code = "TENANT_REQUIRED", message = "Tenant context is required." });
    }
}
