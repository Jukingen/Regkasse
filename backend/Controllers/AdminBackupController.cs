using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Backup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

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
    private readonly IBackupRecoverabilitySummaryService _recoverabilitySummary;
    private readonly IRestoreOrchestrationBoundary _restore;
    private readonly IBackupOperationalReadiness _readiness;
    private readonly IOptionsMonitor<BackupOptions> _backupOptions;

    public AdminBackupController(
        IBackupManualTriggerService trigger,
        IBackupRunQueryService query,
        IBackupRecoverabilitySummaryService recoverabilitySummary,
        IRestoreOrchestrationBoundary restore,
        IBackupOperationalReadiness readiness,
        IOptionsMonitor<BackupOptions> backupOptions)
    {
        _trigger = trigger;
        _query = query;
        _recoverabilitySummary = recoverabilitySummary;
        _restore = restore;
        _readiness = readiness;
        _backupOptions = backupOptions;
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

        var artifactPolicy = _readiness.GetArtifactPipelinePolicy();
        var dto = BackupTriggerResponseFactory.Create(
            outcome,
            artifactPolicy,
            _backupOptions.CurrentValue.AutomaticRetryMaxAttempts);

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
        var artifactPolicy = _readiness.GetArtifactPipelinePolicy();
        return Ok(new BackupLatestStatusResponseDto
        {
            LatestRun = latest == null
                ? null
                : BackupRunMapper.ToDto(
                    latest,
                    includeChildren: true,
                    pipelinePolicy: artifactPolicy,
                    materializedChildren: true,
                    automaticRetryMaxAttemptsBudget: _backupOptions.CurrentValue.AutomaticRetryMaxAttempts),
            Restore = new RestoreCapabilityDto
            {
                IsAutomatedRestoreAvailable = cap.IsAutomatedRestoreAvailable,
                Notes = cap.Notes
            },
            ConfigurationHealth = BackupConfigurationHealthResponseMapper.FromSnapshot(cfg),
            ArtifactPipelinePolicy = BackupArtifactPipelinePolicyMapper.ToDto(artifactPolicy)
        });
    }

    /// <summary>
    /// Recoverability proof özeti (first-class): en son backup isteği, son başarılı yedek, son geçen artifact doğrulama,
    /// son başarılı <em>zamanlanmış</em> restore drill kanıtı ve UTC yaşı (kanıt yoksa null).
    /// </summary>
    [HttpGet("recoverability-summary")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(BackupRecoverabilitySummaryResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BackupRecoverabilitySummaryResponseDto>> GetRecoverabilitySummary(
        CancellationToken cancellationToken)
    {
        var dto = await _recoverabilitySummary.GetAsync(cancellationToken);
        return Ok(dto);
    }

    [HttpGet("runs")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<BackupHistoryResponseDto>> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _query.GetHistoryAsync(page, pageSize, cancellationToken);
        var artifactPolicy = _readiness.GetArtifactPipelinePolicy();
        return Ok(new BackupHistoryResponseDto
        {
            Items = items.Select(r => BackupRunMapper.ToDto(
                    r,
                    includeChildren: false,
                    pipelinePolicy: artifactPolicy,
                    materializedChildren: false,
                    automaticRetryMaxAttemptsBudget: _backupOptions.CurrentValue.AutomaticRetryMaxAttempts))
                .ToList(),
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
        var artifactPolicy = _readiness.GetArtifactPipelinePolicy();
        return Ok(BackupRunMapper.ToDto(
            run,
            includeChildren: true,
            pipelinePolicy: artifactPolicy,
            materializedChildren: true,
            automaticRetryMaxAttemptsBudget: _backupOptions.CurrentValue.AutomaticRetryMaxAttempts));
    }

    [HttpGet("verification/latest")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<BackupVerificationResponseDto?>> GetLatestVerification(
        CancellationToken cancellationToken)
    {
        var v = await _query.GetLatestVerificationAsync(cancellationToken);
        if (v == null)
            return Ok(null);
        var completenessRequired = v.BackupRun != null
            && BackupCompletenessSuccessPolicy.TryParseAdapterKind(v.BackupRun.AdapterKind, out var ak)
            && BackupCompletenessSuccessPolicy.CompletenessRequiredForSucceededRun(ak);
        return Ok(new BackupVerificationResponseDto
        {
            Id = v.Id,
            BackupRunId = v.BackupRunId,
            Status = v.Status,
            StartedAt = v.StartedAt,
            CompletedAt = v.CompletedAt,
            VerifierSource = v.VerifierSource,
            CompletenessFlag = v.CompletenessFlag,
            CompletenessRequiredForTerminalSuccess = completenessRequired,
            FailureReason = v.FailureReason
        });
    }
}
