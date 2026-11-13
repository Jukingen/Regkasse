using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin retention policy (Hot/Warm/Cold + 7-year legal hold). Read is settings.view.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/backup")]
[Produces("application/json")]
public sealed class AdminBackupRetentionController : ControllerBase
{
    private readonly IBackupRetentionPolicyService _policy;
    private readonly IBackupColdArchiveService _coldArchive;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IAuditLogService _audit;
    private readonly Data.AppDbContext _db;

    public AdminBackupRetentionController(
        IBackupRetentionPolicyService policy,
        IBackupColdArchiveService coldArchive,
        ICurrentTenantAccessor tenantAccessor,
        IAuditLogService audit,
        Data.AppDbContext db)
    {
        _policy = policy;
        _coldArchive = coldArchive;
        _tenantAccessor = tenantAccessor;
        _audit = audit;
        _db = db;
    }

    [HttpGet("retention-policy")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(BackupRetentionPolicyResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BackupRetentionPolicyResponseDto>> GetPolicy(CancellationToken cancellationToken)
    {
        var scope = BuildRunAccessScope();
        return Ok(await _policy.GetAsync(includeCosts: true, scope, cancellationToken));
    }

    [HttpPut("retention-policy")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(BackupRetentionPolicyResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BackupRetentionPolicyResponseDto>> PutPolicy(
        [FromBody] BackupRetentionPolicyPutRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRole(Roles.SuperAdmin))
            return Forbid();
        if (body == null)
            return BadRequest(new { code = "INVALID_BODY", message = "Request body is required." });

        try
        {
            var dto = await _policy.UpdateAsync(
                body,
                User.GetActorUserId(),
                User.GetActorRole() ?? Roles.SuperAdmin,
                cancellationToken);
            return Ok(dto);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { code = "INVALID_RETENTION_WINDOW", message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "INVALID_RETENTION_POLICY", message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/move-to-cold")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(BackupMoveToColdResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BackupMoveToColdResponseDto>> MoveToCold(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRole(Roles.SuperAdmin))
            return Forbid();

        var run = await _db.BackupRuns
            .Include(r => r.Artifacts)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (run == null)
            return NotFound();

        var result = await _coldArchive.MoveRunToColdAsync(
            run,
            User.GetActorUserId(),
            User.GetActorRole() ?? Roles.SuperAdmin,
            cancellationToken);
        if (!result.Success && result.Message != null
            && result.Message.Contains("disabled", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { code = "COLD_STORAGE_DISABLED", message = result.Message });
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/legal-hold")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(BackupRetentionStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BackupRetentionStatusDto>> SetLegalHold(
        Guid id,
        [FromBody] BackupLegalHoldRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRole(Roles.SuperAdmin))
            return Forbid();

        var run = await _db.BackupRuns
            .Include(r => r.Artifacts)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (run == null)
            return NotFound();

        var enable = body?.LegalHold ?? true;
        var previous = new { run.LegalHold, run.LegalHoldUntilUtc, run.LegalHoldReason };
        run.LegalHold = enable;
        run.LegalHoldReason = string.IsNullOrWhiteSpace(body?.Reason)
            ? (enable ? "Operator legal hold" : "Legal hold cleared")
            : body!.Reason!.Trim();
        run.LegalHoldUntilUtc = enable
            ? body?.UntilUtc ?? run.LegalHoldUntilUtc ?? DateTime.UtcNow.AddYears(BackupStrategyPolicy.SystemLegalRetentionYears)
            : null;
        run.LegalHoldSetByUserId = User.GetActorUserId();
        run.LegalHoldSetAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogSystemOperationAsync(
            action: "BACKUP_LEGAL_HOLD_CHANGED",
            entityType: "BackupRun",
            userId: User.GetActorUserId() ?? "unknown",
            userRole: User.GetActorRole() ?? Roles.SuperAdmin,
            description: enable ? "Backup legal hold enabled." : "Backup legal hold cleared.",
            actionType: AuditEventType.BackupLegalHoldChanged,
            entityId: run.Id,
            oldValues: previous,
            newValues: new { run.LegalHold, run.LegalHoldUntilUtc, run.LegalHoldReason });

        var snap = await _policy.GetSnapshotAsync(cancellationToken);
        return Ok(BackupRetentionStatusEvaluator.FromRun(run, snap));
    }

    private BackupRunAccessScope BuildRunAccessScope() =>
        new(User.IsInRole(Roles.SuperAdmin), _tenantAccessor.TenantId, User.GetActorUserId());
}
