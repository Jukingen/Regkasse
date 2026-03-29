using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Backup;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Backup orchestration control plane: enqueue + read models. Execution runs in <see cref="BackupOrchestratorHostedService"/>.
/// Artifact verification (checksum/staging) is separate from restore drills; see <see cref="AdminRestoreVerificationController"/>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/backup")]
[Produces("application/json")]
public sealed class AdminBackupController : ControllerBase
{
    private readonly IBackupManualTriggerService _trigger;
    private readonly IBackupRunQueryService _query;
    private readonly IRestoreOrchestrationBoundary _restore;
    private readonly IBackupOperationalReadiness _readiness;

    public AdminBackupController(
        IBackupManualTriggerService trigger,
        IBackupRunQueryService query,
        IRestoreOrchestrationBoundary restore,
        IBackupOperationalReadiness readiness)
    {
        _trigger = trigger;
        _query = query;
        _restore = restore;
        _readiness = readiness;
    }

    /// <summary>Enqueue manual backup (HTTP thread does not run pg_dump / file IO).</summary>
    [HttpPost("trigger")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(BackupTriggerResponseDto), 202)]
    [ProducesResponseType(typeof(BackupTriggerResponseDto), 200)]
    public async Task<ActionResult<BackupTriggerResponseDto>> TriggerManual(
        [FromBody] BackupTriggerRequestDto? body,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        var role = User.GetActorRole() ?? "Unknown";
        var correlationId = HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
        var outcome = await _trigger.RequestManualBackupAsync(
            userId,
            role,
            body?.IdempotencyKey,
            correlationId,
            cancellationToken);

        var dto = BackupTriggerResponseFactory.Create(outcome);

        if (outcome.Kind is BackupManualTriggerResultKind.DuplicateActiveManualPrevented
            or BackupManualTriggerResultKind.IdempotentReplay)
            return Ok(dto);

        return AcceptedAtAction(nameof(GetRunById), new { id = outcome.Run.Id }, dto);
    }

    [HttpGet("status/latest")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<BackupLatestStatusResponseDto>> GetLatestStatus(CancellationToken cancellationToken)
    {
        var latest = await _query.GetLatestRunAsync(cancellationToken);
        var cap = _restore.DescribeCapabilities();
        var cfg = _readiness.GetConfigurationHealth();
        var pipeline = _readiness.GetArtifactPipelinePolicy();
        return Ok(new BackupLatestStatusResponseDto
        {
            LatestRun = latest == null ? null : BackupRunMapper.ToDto(latest, includeChildren: false),
            Restore = new RestoreCapabilityDto
            {
                IsAutomatedRestoreAvailable = cap.IsAutomatedRestoreAvailable,
                Notes = cap.Notes
            },
            ConfigurationHealth = new BackupConfigurationHealthResponseDto
            {
                Level = cfg.Level.ToString(),
                Issues = cfg.Issues,
                EffectiveAdapterKind = cfg.EffectiveAdapterKind.ToString(),
                WorkerEnabled = cfg.WorkerEnabled,
                ArtifactVerificationDisclaimer = cfg.ArtifactVerificationDisclaimer
            },
            ArtifactPipelinePolicy = BackupArtifactPipelinePolicyMapper.ToDto(pipeline)
        });
    }

    [HttpGet("runs")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<BackupHistoryResponseDto>> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _query.GetHistoryAsync(page, pageSize, cancellationToken);
        return Ok(new BackupHistoryResponseDto
        {
            Items = items.Select(r => BackupRunMapper.ToDto(r, includeChildren: false)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    [HttpGet("runs/{id:guid}")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<BackupRunResponseDto>> GetRunById(Guid id, CancellationToken cancellationToken)
    {
        var run = await _query.GetByIdAsync(id, cancellationToken);
        if (run == null)
            return NotFound();
        return Ok(BackupRunMapper.ToDto(run, includeChildren: true));
    }

    [HttpGet("verification/latest")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<BackupVerificationResponseDto?>> GetLatestVerification(
        CancellationToken cancellationToken)
    {
        var v = await _query.GetLatestVerificationAsync(cancellationToken);
        if (v == null)
            return Ok(null);
        return Ok(new BackupVerificationResponseDto
        {
            Id = v.Id,
            BackupRunId = v.BackupRunId,
            Status = v.Status,
            StartedAt = v.StartedAt,
            CompletedAt = v.CompletedAt,
            VerifierSource = v.VerifierSource,
            CompletenessFlag = v.CompletenessFlag,
            FailureReason = v.FailureReason
        });
    }
}
