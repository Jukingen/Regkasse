using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    private readonly IBackupRunService _backupRunService;
    private readonly IBackupRecoverabilitySummaryService _recoverabilitySummary;
    private readonly IRestoreOrchestrationBoundary _restore;
    private readonly IBackupOperationalReadiness _readiness;
    private readonly IOptionsMonitor<BackupOptions> _backupOptions;
    private readonly IBackupArtifactDownloadService _artifactDownload;
    private readonly IAuditLogService _audit;
    private readonly ILogger<AdminBackupController> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly AppDbContext _db;
    private readonly IBackupSettingsAdminService _backupSettings;
    private readonly IBackupDashboardStatsService _dashboardStats;
    private readonly IPitrService _pitr;
    private readonly IBackupVerificationReportService _verificationReport;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminBackupController(
        IBackupManualTriggerService trigger,
        IBackupRunQueryService query,
        IBackupRunService backupRunService,
        IBackupRecoverabilitySummaryService recoverabilitySummary,
        IRestoreOrchestrationBoundary restore,
        IBackupOperationalReadiness readiness,
        IOptionsMonitor<BackupOptions> backupOptions,
        IBackupArtifactDownloadService artifactDownload,
        IAuditLogService audit,
        ILogger<AdminBackupController> logger,
        IHostEnvironment hostEnvironment,
        AppDbContext db,
        IBackupSettingsAdminService backupSettings,
        IBackupDashboardStatsService dashboardStats,
        IPitrService pitr,
        IBackupVerificationReportService verificationReport,
        ICurrentTenantAccessor tenantAccessor)
    {
        _trigger = trigger;
        _query = query;
        _backupRunService = backupRunService;
        _recoverabilitySummary = recoverabilitySummary;
        _restore = restore;
        _readiness = readiness;
        _backupOptions = backupOptions;
        _artifactDownload = artifactDownload;
        _audit = audit;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
        _db = db;
        _backupSettings = backupSettings;
        _dashboardStats = dashboardStats;
        _pitr = pitr;
        _verificationReport = verificationReport;
        _tenantAccessor = tenantAccessor;
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

    [HttpGet("settings")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(BackupSettingsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BackupSettingsResponseDto>> GetAutomationSettings(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _backupSettings.GetAsync(cancellationToken));
        }
        catch (InvalidOperationException ex) when (ex.Message == "TENANT_CONTEXT_REQUIRED")
        {
            return BadRequest(new { code = "TENANT_CONTEXT_REQUIRED", message = "Tenant context is required for backup schedule settings." });
        }
    }

    /// <summary>Updates singleton automation settings (cron is UTC CronFormat.Standard).</summary>
    [HttpPut("settings")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(BackupSettingsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BackupSettingsResponseDto>> PutAutomationSettings(
        [FromBody] BackupSettingsPutRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (body == null)
            return BadRequest(new { code = "BACKUP_SETTINGS_BODY_REQUIRED", message = "Request body is required." });

        try
        {
            var updated = await _backupSettings.PutAsync(body, cancellationToken);
            return Ok(updated);
        }
        catch (InvalidOperationException ex) when (ex.Message == "TENANT_CONTEXT_REQUIRED")
        {
            return BadRequest(new { code = "TENANT_CONTEXT_REQUIRED", message = "Tenant context is required for backup schedule settings." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "BACKUP_SETTINGS_INVALID", message = ex.Message });
        }
    }

    [HttpGet("schedule/status")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(BackupScheduleStatusResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BackupScheduleStatusResponseDto>> GetScheduleStatus(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _backupSettings.GetScheduleStatusAsync(cancellationToken));
        }
        catch (InvalidOperationException ex) when (ex.Message == "TENANT_CONTEXT_REQUIRED")
        {
            return BadRequest(new { code = "TENANT_CONTEXT_REQUIRED", message = "Tenant context is required for backup schedule settings." });
        }
    }

    /// <summary>Monitoring dashboard aggregates (30-day success rate, RPO/RTO, history series).</summary>
    [HttpGet("dashboard/stats")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(BackupDashboardStatsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BackupDashboardStatsResponseDto>> GetDashboardStats(
        CancellationToken cancellationToken)
        => Ok(await _dashboardStats.GetAsync(cancellationToken));

    [HttpGet("status/latest")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<BackupLatestStatusResponseDto>> GetLatestStatus(CancellationToken cancellationToken)
    {
        var latest = await _query.GetLatestRunAsync(cancellationToken);
        var durationStats = await _query.GetAverageSucceededDurationAsync(15, cancellationToken);
        var cap = _restore.DescribeCapabilities();
        var cfg = _readiness.GetConfigurationHealth();
        var artifactPolicy = _readiness.GetArtifactPipelinePolicy();
        var downloadEnrichment = new BackupDownloadEnrichment(
            _backupOptions.CurrentValue,
            _hostEnvironment,
            _logger);
        return Ok(new BackupLatestStatusResponseDto
        {
            LatestRun = latest == null
                ? null
                : BackupRunMapper.ToDto(
                    latest,
                    includeChildren: true,
                    pipelinePolicy: artifactPolicy,
                    materializedChildren: true,
                    automaticRetryMaxAttemptsBudget: _backupOptions.CurrentValue.AutomaticRetryMaxAttempts,
                    downloadEnrichment: downloadEnrichment),
            Restore = new RestoreCapabilityDto
            {
                IsAutomatedRestoreAvailable = cap.IsAutomatedRestoreAvailable,
                Notes = cap.Notes
            },
            ConfigurationHealth = BackupConfigurationHealthResponseMapper.FromSnapshot(cfg),
            ArtifactPipelinePolicy = BackupArtifactPipelinePolicyMapper.ToDto(artifactPolicy),
            AverageSucceededBackupDurationSeconds = durationStats.AverageDurationSeconds,
            AverageSucceededBackupDurationSampleCount = durationStats.SampleCount
        });
    }

    /// <summary>
    /// Başarılı yedek çalıştırmasına ait artefakt dosyasını indirir (staging veya harici arşiv); yalnızca tamamlanmış başarılı koşular.
    /// </summary>
    [HttpGet("runs/{runId:guid}/artifacts/{artifactId:guid}/download")]
    [HasPermission(AppPermissions.SettingsManage)]
    [Produces("application/octet-stream", "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DownloadArtifact(
        Guid runId,
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        var prepare = await _artifactDownload.PrepareDownloadAsync(runId, artifactId, cancellationToken);
        switch (prepare.Status)
        {
            case BackupArtifactDownloadPrepareStatus.Ok:
                break;
            case BackupArtifactDownloadPrepareStatus.RunNotFound:
                return NotFound(new
                {
                    code = "BACKUP_RUN_NOT_FOUND",
                    message = "Backup run does not exist."
                });
            case BackupArtifactDownloadPrepareStatus.ArtifactNotFound:
                return NotFound(new
                {
                    code = "BACKUP_ARTIFACT_NOT_FOUND",
                    message = "Artifact does not exist for this backup run."
                });
            case BackupArtifactDownloadPrepareStatus.RunNotSucceeded:
                return Conflict(new { code = "BACKUP_RUN_NOT_SUCCEEDED", message = "Backup run is not in Succeeded state." });
            case BackupArtifactDownloadPrepareStatus.FileNotOnDisk:
                return NotFound(new { code = "BACKUP_ARTIFACT_FILE_MISSING", message = "Artifact file is not available on the server." });
            case BackupArtifactDownloadPrepareStatus.InvalidConfiguration:
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { code = "BACKUP_STORAGE_NOT_CONFIGURED", message = "Backup staging/archive paths are not configured." });
            case BackupArtifactDownloadPrepareStatus.SimulatedExecutionNotDownloadable:
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    code = "BACKUP_ARTIFACT_NOT_DOWNLOADABLE_SIMULATED",
                    message = "Download blocked as simulated (legacy path). Prefer current API: Fake/ProductionStub files download when present on disk."
                });
            default:
                return NotFound(new { code = "BACKUP_DOWNLOAD_UNKNOWN", message = "Download could not be prepared." });
        }

        var userId = User.GetActorUserId() ?? "unknown";
        var role = User.GetActorRole() ?? "Unknown";
        var correlationId = HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
        try
        {
            await _audit.LogSystemOperationAsync(
                action: "BACKUP_ARTIFACT_DOWNLOAD",
                entityType: "BackupArtifact",
                userId: userId,
                userRole: role,
                description: $"Backup artifact download (run={runId}, artifact={artifactId}).",
                notes: null,
                status: AuditLogStatus.Success,
                errorDetails: null,
                requestData: new { backupRunId = runId, artifactId },
                responseData: new { downloadFileName = prepare.DownloadFileName },
                correlationIdOverride: correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Audit log failed after backup artifact download was prepared; returning file response anyway. runId={RunId}, artifactId={ArtifactId}",
                runId, artifactId);
        }

        var file = new PhysicalFileResult(prepare.AbsolutePath!, "application/octet-stream")
        {
            FileDownloadName = prepare.DownloadFileName,
            EnableRangeProcessing = true
        };
        return file;
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

    /// <summary>Point-in-time recovery window (base backups + declared WAL archiving).</summary>
    [HttpGet("pitr/availability")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(PitrAvailabilityResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PitrAvailabilityResponseDto>> GetPitrAvailability(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (!tenantId.HasValue)
            return BadRequest(new { message = "Tenant context is required.", code = "TENANT_REQUIRED" });

        var dto = await _pitr.GetPitrAvailabilityAsync(tenantId.Value, cancellationToken);
        return Ok(dto);
    }

    /// <summary>Validate a target UTC restore point against base backups and WAL coverage.</summary>
    [HttpPost("pitr/validate")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(RestorePointValidationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RestorePointValidationResultDto>> ValidatePitrRestorePoint(
        [FromBody] ValidatePitrRestorePointRequestDto? body,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (!tenantId.HasValue)
            return BadRequest(new { message = "Tenant context is required.", code = "TENANT_REQUIRED" });

        if (body == null)
            return BadRequest(new { message = "Request body is required." });

        var dto = await _pitr.ValidateRestorePointAsync(
            tenantId.Value,
            body.TargetTimeUtc,
            cancellationToken);
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
                    includeChildren: true,
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
        var artifactPolicy = _readiness.GetArtifactPipelinePolicy();
        var downloadEnrichment = new BackupDownloadEnrichment(
            _backupOptions.CurrentValue,
            _hostEnvironment,
            _logger);
        var dto = await _backupRunService.GetBackupRunAsync(
            id,
            new BackupRunDtoMappingOptions
            {
                PipelinePolicy = artifactPolicy,
                AutomaticRetryMaxAttemptsBudget = _backupOptions.CurrentValue.AutomaticRetryMaxAttempts,
                DownloadEnrichment = downloadEnrichment,
            },
            cancellationToken);
        if (dto == null)
            return NotFound();
        return Ok(dto);
    }

    /// <summary>Detailed verification report: logical dump TOC vs live table row counts.</summary>
    [HttpGet("runs/{id:guid}/verification-report")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(BackupVerificationReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BackupVerificationReportDto>> GetVerificationReport(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var report = await _verificationReport.GenerateReportAsync(id, cancellationToken);
            return Ok(report);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
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

    /// <summary>Kalıcı yedek çalıştırma modu + etkin sağlık (Fake / PgDump / yapılandırmayı izle).</summary>
    [HttpGet("execution-mode")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(BackupExecutionModeResponseDto), StatusCodes.Status200OK)]
    public ActionResult<BackupExecutionModeResponseDto> GetExecutionMode()
    {
        var snap = _readiness.GetConfigurationHealth();
        var dto = BuildExecutionModeResponse(snap);
        return Ok(dto);
    }

    /// <summary>Admin yedek modunu kalıcı olarak günceller; PgDump önkoşulları Unhealthy ise kayıt yapılmaz.</summary>
    [HttpPut("execution-mode")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(BackupExecutionModeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BackupExecutionModeResponseDto>> PutExecutionMode(
        [FromBody] BackupExecutionModePutRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.Mode))
        {
            return BadRequest(new
            {
                code = "BACKUP_EXECUTION_MODE_MISSING",
                message =
                    "Request body must include a non-empty Mode: UseConfigurationDefault (inherit config), Fake, or RealPgDump (aliases and internal enum names are accepted)."
            });
        }

        if (!BackupExecutionModeApiMapper.TryParseAdminMode(body.Mode, out var parsed, out var parseError))
        {
            return BadRequest(new
            {
                code = "BACKUP_EXECUTION_MODE_INVALID",
                message = parseError ?? "Invalid mode."
            });
        }

        var opts = _backupOptions.CurrentValue;
        if (parsed == AdminBackupRuntimeExecutionMode.SimulatedFake
            && BackupConfigurationEvaluation.IsProductionLikeEnvironment(_hostEnvironment))
        {
            if (!opts.AcknowledgeFakeBackupAdapterOutsideDevelopment)
            {
                return Conflict(new
                {
                    code = "BACKUP_SIMULATED_FAKE_FORBIDDEN_PRODUCTION",
                    message =
                        "SimulatedFake is not allowed in production-like environments until Backup:AcknowledgeFakeBackupAdapterOutsideDevelopment=true is set in configuration (explicit operator intent at deployment level)."
                });
            }

            if (!body.ConfirmSimulatedOnlyOperationalRiskInProduction)
            {
                return BadRequest(new
                {
                    code = "BACKUP_SIMULATED_FAKE_CONFIRMATION_REQUIRED",
                    message =
                        "In production-like environments, set ConfirmSimulatedOnlyOperationalRiskInProduction=true to acknowledge simulated-only backups (no PostgreSQL logical dump)."
                });
            }
        }

        var preview = _readiness.GetConfigurationHealthAssumingAdminMode(parsed);
        if (parsed == AdminBackupRuntimeExecutionMode.PostgreSqlPgDump
            && preview.Level == BackupConfigurationHealthLevel.Unhealthy)
        {
            return UnprocessableEntity(new
            {
                code = "BACKUP_PG_DUMP_PREREQUISITES_UNHEALTHY",
                message = "Real (PgDump) mode cannot be saved while configuration health is Unhealthy; fix blockers first.",
                issues = preview.Issues
            });
        }

        var userId = User.GetActorUserId();
        var role = User.GetActorRole() ?? "Unknown";
        var correlationId = HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
        var now = DateTime.UtcNow;

        var row = await _db.BackupRuntimeExecutionPreferences
            .FirstOrDefaultAsync(x => x.Id == BackupRuntimeExecutionPreference.SingletonId, cancellationToken);
        if (row == null)
        {
            row = new BackupRuntimeExecutionPreference { Id = BackupRuntimeExecutionPreference.SingletonId };
            _db.BackupRuntimeExecutionPreferences.Add(row);
        }

        row.Mode = parsed;
        row.UpdatedAtUtc = now;
        row.UpdatedByUserId = userId;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogSystemOperationAsync(
            action: "BACKUP_RUNTIME_EXECUTION_MODE_CHANGED",
            entityType: "BackupRuntimeExecutionPreference",
            userId: userId ?? "unknown",
            userRole: role,
            description: $"Backup runtime execution mode set to {parsed}.",
            notes: null,
            status: AuditLogStatus.Success,
            errorDetails: null,
            requestData: new { mode = parsed.ToString(), body.ConfirmSimulatedOnlyOperationalRiskInProduction },
            responseData: null,
            correlationIdOverride: correlationId);

        var after = _readiness.GetConfigurationHealth();
        var responseDto = BuildExecutionModeResponse(after);
        _logger.LogInformation(
            "Backup execution mode updated: storedInternal={StoredInternal}, userFacing={UserFacing}, effectiveAdapter={Effective}, summary={Summary}",
            parsed.ToString(),
            responseDto.RequestedUserFacingMode,
            responseDto.EffectiveExecutionAdapterKind,
            responseDto.EffectiveModeResolutionSummaryEnglish);
        return Ok(responseDto);
    }

    private BackupExecutionModeResponseDto BuildExecutionModeResponse(BackupConfigurationHealthSnapshot snap) =>
        BackupExecutionModeResponseBuilder.Build(
            snap,
            _backupOptions.CurrentValue,
            _hostEnvironment,
            _readiness);
}
