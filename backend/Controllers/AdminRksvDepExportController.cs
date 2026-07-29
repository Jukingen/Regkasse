using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FeatureFlags;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// RKSV §7 DEP export in BMF JSON format (Anlage Z3). Distinct from operational <see cref="FiscalExportController"/>.
/// </summary>
[ApiController]
[Route("api/admin/rksv/dep-export")]
[Authorize]
[HasPermission(AppPermissions.ReportExport)]
[HasPermission(AppPermissions.AuditView)]
public class AdminRksvDepExportController : ControllerBase
{
    private readonly IRksvDepExportService _depExportService;
    private readonly IDepExportHistoryService _historyService;
    private readonly IDepExportScheduler _scheduler;
    private readonly IDepExportRequirementService _requirementService;
    private readonly IDepExportComplianceScoreService _scoreService;
    private readonly IDepExportStatisticsService _statisticsService;
    private readonly IDepExportValidationService _validationService;
    private readonly IDepExportArchiveService _archiveService;
    private readonly IDepExportPushNotificationService _pushNotification;
    private readonly IDepExportAuditService _depExportAudit;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IRksvEnvironmentService _rksvEnv;
    private readonly IDownloadHistoryService _downloadHistory;
    private readonly IFeatureFlagService _featureFlags;

    public AdminRksvDepExportController(
        IRksvDepExportService depExportService,
        IDepExportHistoryService historyService,
        IDepExportScheduler scheduler,
        IDepExportRequirementService requirementService,
        IDepExportComplianceScoreService scoreService,
        IDepExportStatisticsService statisticsService,
        IDepExportValidationService validationService,
        IDepExportArchiveService archiveService,
        IDepExportPushNotificationService pushNotification,
        IDepExportAuditService depExportAudit,
        ICurrentTenantAccessor tenantAccessor,
        IRksvEnvironmentService rksvEnv,
        IDownloadHistoryService downloadHistory,
        IFeatureFlagService featureFlags)
    {
        _depExportService = depExportService;
        _historyService = historyService;
        _scheduler = scheduler;
        _requirementService = requirementService;
        _scoreService = scoreService;
        _statisticsService = statisticsService;
        _validationService = validationService;
        _archiveService = archiveService;
        _pushNotification = pushNotification;
        _depExportAudit = depExportAudit;
        _tenantAccessor = tenantAccessor;
        _rksvEnv = rksvEnv;
        _downloadHistory = downloadHistory;
        _featureFlags = featureFlags;
    }

    [HttpGet]
    [ProducesResponseType(typeof(RksvDepExportRootDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RksvDepExportEnvelopeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDepExport(
        [FromQuery] Guid cashRegisterId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromQuery] bool includeSpecialReceipts = true,
        [FromQuery] bool includeDailyClosings = true,
        [FromQuery] bool includeEnvelope = false,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        try
        {
            var fileName = await _historyService
                .BuildFileNameAsync(tenantId.Value, cashRegisterId, cancellationToken)
                .ConfigureAwait(false);
            Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";

            if (_featureFlags.IsEnabled(
                    FeatureFlagNames.EnableDepExportV2,
                    tenantId.Value.ToString("D")))
            {
                // V2 rollout: response metadata only (BMF JSON body stays schema-compatible).
                Response.Headers["X-Regkasse-Dep-Export-Schema"] = "v2";
            }

            if (includeEnvelope)
            {
                var build = await _depExportService.GenerateDepExportWithValidationAsync(
                        cashRegisterId,
                        fromUtc,
                        toUtc,
                        includeSpecialReceipts,
                        includeDailyClosings,
                        cancellationToken)
                    .ConfigureAwait(false);

                var exportJson = System.Text.Json.JsonSerializer.Serialize(build.Root);
                var validation = await _depExportService
                    .ValidateExportFormatAsync(exportJson, cancellationToken)
                    .ConfigureAwait(false);

                await RecordCompletedExportAsync(
                        tenantId.Value,
                        cashRegisterId,
                        fromUtc,
                        toUtc,
                        includeSpecialReceipts,
                        includeDailyClosings,
                        build.Root,
                        fileName,
                        cancellationToken)
                    .ConfigureAwait(false);

                return Ok(new RksvDepExportEnvelopeDto
                {
                    LegalNotice = build.LegalNotice,
                    Dep = build.Root,
                    BelegCount = build.BelegCount,
                    BelegeGruppeCount = build.BelegeGruppeCount,
                    CashRegisterId = build.CashRegisterId,
                    RegisterNumber = build.RegisterNumber,
                    FromUtc = build.FromUtc,
                    ToUtc = build.ToUtc,
                    IsDemo = build.IsDemo,
                    Environment = build.Environment,
                    FormatValidated = build.FormatValidated,
                    FormatValidation = validation,
                    LegacyJwsCount = build.LegacyJwsCount,
                    F5CompliantJwsCount = build.F5CompliantJwsCount,
                    LegacyJwsWarning = build.LegacyJwsWarning,
                    PrueftoolCompatible = build.PrueftoolCompatible,
                });
            }

            var export = await _depExportService.GenerateDepExportAsync(
                    cashRegisterId,
                    fromUtc,
                    toUtc,
                    includeSpecialReceipts,
                    includeDailyClosings,
                    cancellationToken)
                .ConfigureAwait(false);

            await RecordCompletedExportAsync(
                    tenantId.Value,
                    cashRegisterId,
                    fromUtc,
                    toUtc,
                    includeSpecialReceipts,
                    includeDailyClosings,
                    export,
                    fileName,
                    cancellationToken)
                .ConfigureAwait(false);

            return Ok(export);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, code = "RKSV_DEP_EXPORT_INVALID_RANGE" });
        }
        catch (RksvDepExportCertificateMissingException ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message = ex.Message,
                    code = RksvDepExportCertificateMissingException.ErrorCode,
                    thumbprint = ex.Thumbprint,
                });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message, code = "RKSV_DEP_EXPORT_REGISTER_NOT_FOUND" });
        }
    }

    [HttpPost("validate")]
    [HasPermission(AppPermissions.ReportExport)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateExport(
        [FromBody] ValidateExportRequest request,
        CancellationToken ct = default)
    {
        if (_tenantAccessor.TenantId is null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.ExportJson))
            return BadRequest(new { message = "exportJson is required.", code = "RKSV_DEP_EXPORT_JSON_REQUIRED" });

        var result = await _depExportService.ValidateExportFormatAsync(request.ExportJson, ct).ConfigureAwait(false);

        return Ok(new
        {
            success = result.IsValid,
            message = result.IsValid ? "Export format is valid" : "Export format is invalid",
            environment = _rksvEnv.GetEnvironmentDisplayName(),
            validation = result,
        });
    }

    [HttpPost("test-prueftool")]
    [HasPermission(AppPermissions.ReportExport)]
    [ProducesResponseType(typeof(RksvDepPrueftoolResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TestPrueftool(
        [FromBody] TestPrueftoolRequest request,
        CancellationToken ct = default)
    {
        if (_tenantAccessor.TenantId is null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.ExportJson))
            return BadRequest(new { message = "exportJson is required.", code = "RKSV_DEP_EXPORT_JSON_REQUIRED" });

        var result = await _depExportService
            .RunPrueftoolAsync(request.ExportJson, ct)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("test-material")]
    [ProducesResponseType(typeof(CryptoMaterialDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTestMaterial(
        [FromQuery] Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is null)
            return NotFound();

        try
        {
            var material = await _depExportService
                .GenerateCryptoMaterialAsync(cashRegisterId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(material);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message, code = "RKSV_DEP_EXPORT_REGISTER_NOT_FOUND" });
        }
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(DepExportHistoryListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListHistory(
        [FromQuery] Guid? cashRegisterId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var result = await _historyService
            .ListAsync(tenantId.Value, cashRegisterId, page, pageSize, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("history/{id:guid}")]
    [ProducesResponseType(typeof(DepExportHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is null)
            return NotFound();

        var row = await _historyService.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpGet("history/{id:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadHistory(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var file = await _historyService.TryOpenDownloadAsync(id, cancellationToken).ConfigureAwait(false);
        if (file is null)
            return NotFound(new { message = "Stored export file not available.", code = "RKSV_DEP_EXPORT_FILE_NOT_FOUND" });

        var (stream, fileName, contentType) = file.Value;
        try
        {
            await _downloadHistory.RecordAsync(
                    new DownloadHistoryRecordRequest
                    {
                        TenantId = tenantId.Value,
                        UserId = User.GetActorUserId() ?? "unknown",
                        FileName = fileName,
                        FileType = "json",
                        FileSize = stream.CanSeek ? stream.Length : null,
                        DownloadUrl = $"/api/admin/rksv/dep-export/history/{id}/download",
                        IpAddress = ResolveClientIpAddress(),
                        UserAgent = ResolveUserAgent(),
                        SourceKind = "dep-export",
                        SourceId = id,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // Download must still succeed if history write fails.
        }

        try
        {
            await _depExportAudit.LogExportActionAsync(
                    new DepExportAuditEntry
                    {
                        TenantId = tenantId.Value,
                        Action = DepExportAuditActions.Downloaded,
                        ExportName = fileName,
                        ExportHistoryId = id,
                        UserId = User.GetActorUserId() ?? "unknown",
                        UserRole = User.GetActorRole() ?? "Unknown",
                        UserEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
                        IpAddress = ResolveClientIpAddress(),
                        UserAgent = ResolveUserAgent(),
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // Download must still succeed if audit write fails.
        }

        return File(stream, contentType, fileName);
    }

    [HttpPost("schedule")]
    [ProducesResponseType(typeof(DepExportScheduleResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSchedule(
        [FromBody] CreateDepExportScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var schedule = await _scheduler.CreateScheduleAsync(
                    tenantId.Value,
                    request.CashRegisterId,
                    request.ScheduleType,
                    request.DayOfMonth,
                    request.TimeOfDay,
                    request.RecipientEmails,
                    cancellationToken)
                .ConfigureAwait(false);

            return Created($"/api/admin/rksv/dep-export/schedules", ToScheduleDto(schedule));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, code = "RKSV_DEP_SCHEDULE_INVALID" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message, code = "RKSV_DEP_EXPORT_REGISTER_NOT_FOUND" });
        }
    }

    [HttpGet("schedules")]
    [ProducesResponseType(typeof(IEnumerable<DepExportScheduleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSchedules(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var schedules = await _scheduler.GetSchedulesAsync(tenantId.Value, cancellationToken).ConfigureAwait(false);
        return Ok(schedules.Select(ToScheduleDto));
    }

    [HttpGet("compliance")]
    [ProducesResponseType(typeof(DepExportComplianceStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetComplianceStatus(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var status = await _requirementService
            .GetComplianceStatusAsync(tenantId.Value, cancellationToken)
            .ConfigureAwait(false);
        return Ok(DepExportComplianceDtoMapper.ToDto(status));
    }

    [HttpGet("requirements")]
    [ProducesResponseType(typeof(IEnumerable<DepExportRequirementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListRequirements(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var requirements = await _requirementService
            .GetRequirementsAsync(tenantId.Value, cancellationToken)
            .ConfigureAwait(false);
        return Ok(requirements.Select(DepExportComplianceDtoMapper.ToDto));
    }

    [HttpGet("requirements/next")]
    [ProducesResponseType(typeof(DepExportRequirementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNextRequirement(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var next = await _requirementService
            .GetNextRequirementAsync(tenantId.Value, cancellationToken)
            .ConfigureAwait(false);
        return next is null ? NoContent() : Ok(DepExportComplianceDtoMapper.ToDto(next));
    }

    [HttpGet("compliance/current-period")]
    [ProducesResponseType(typeof(DepExportCompliancePeriodResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentCompliancePeriod(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var period = await _requirementService
            .GetCurrentPeriodAsync(tenantId.Value, cancellationToken)
            .ConfigureAwait(false);
        return period is null ? NoContent() : Ok(DepExportComplianceDtoMapper.ToDto(period));
    }

    [HttpGet("compliance/score")]
    [ProducesResponseType(typeof(DepExportComplianceScoreDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetComplianceScore(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var score = await _scoreService
            .CalculateScoreAsync(tenantId.Value, cancellationToken)
            .ConfigureAwait(false);
        return Ok(score);
    }

    [HttpGet("compliance/score/history")]
    [ProducesResponseType(typeof(DepExportComplianceScoreHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetComplianceScoreHistory(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var history = await _scoreService
            .GetScoreHistoryAsync(tenantId.Value, cancellationToken)
            .ConfigureAwait(false);
        return Ok(history);
    }

    [HttpGet("compliance/score/suggestions")]
    [ProducesResponseType(typeof(IEnumerable<DepExportImprovementSuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetComplianceScoreSuggestions(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var suggestions = await _scoreService
            .GetImprovementSuggestionsAsync(tenantId.Value, cancellationToken)
            .ConfigureAwait(false);
        return Ok(suggestions);
    }

    /// <summary>Operational DEP export statistics from history (not BMF certification).</summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(DepExportStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var to = toUtc ?? DateTime.UtcNow;
        var from = fromUtc ?? to.AddMonths(-12);
        var stats = await _statisticsService
            .GetStatisticsAsync(tenantId.Value, from, to, cancellationToken)
            .ConfigureAwait(false);
        return Ok(stats);
    }

    [HttpGet("statistics/trend")]
    [ProducesResponseType(typeof(IEnumerable<DepExportTrendPointDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatisticsTrend(
        [FromQuery] int months = 12,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var trend = await _statisticsService
            .GetTrendAsync(tenantId.Value, months, cancellationToken)
            .ConfigureAwait(false);
        return Ok(trend);
    }

    [HttpGet("statistics/forecast")]
    [ProducesResponseType(typeof(DepExportForecastDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatisticsForecast(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var forecast = await _statisticsService
            .GetForecastAsync(tenantId.Value, cancellationToken)
            .ConfigureAwait(false);
        return Ok(forecast);
    }

    [HttpGet("push-notification-settings")]
    [ProducesResponseType(typeof(DepExportMobilePushSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPushNotificationSettings(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var settings = await _pushNotification
            .GetSettingsAsync(tenantId.Value, cancellationToken)
            .ConfigureAwait(false);
        return Ok(settings);
    }

    [HttpPut("push-notification-settings")]
    [ProducesResponseType(typeof(DepExportMobilePushSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SavePushNotificationSettings(
        [FromBody] DepExportMobilePushSettings settings,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var saved = await _pushNotification
            .SaveSettingsAsync(tenantId.Value, settings, cancellationToken)
            .ConfigureAwait(false);
        return Ok(saved);
    }

    [HttpGet("audit-trail")]
    [ProducesResponseType(typeof(IEnumerable<DepExportAuditEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuditTrail(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? action = null,
        [FromQuery] string? userSearch = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var to = toUtc ?? DateTime.UtcNow;
        var from = fromUtc ?? to.AddMonths(-12);
        var entries = await _depExportAudit
            .GetAuditTrailAsync(tenantId.Value, from, to, action, userSearch, limit, cancellationToken)
            .ConfigureAwait(false);
        return Ok(entries);
    }

    [HttpGet("audit-report")]
    [ProducesResponseType(typeof(DepExportAuditReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuditReport(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var report = await _depExportAudit
            .GenerateAuditReportAsync(tenantId.Value, fromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);
        return Ok(report);
    }

    [HttpPost("history/{id:guid}/validate")]
    [ProducesResponseType(typeof(DepExportHistoryValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ValidateHistory(Guid id, CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is null)
            return NotFound();

        var row = await _historyService.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (row is null)
            return NotFound();

        var result = await _validationService
            .ValidateExportAsync(id, exportJson: null, cancellationToken)
            .ConfigureAwait(false);

        if (result.ErrorMessage == "Export not found")
            return NotFound();

        return Ok(result);
    }

    [HttpGet("history/{id:guid}/validation")]
    [ProducesResponseType(typeof(DepExportHistoryValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistoryValidation(Guid id, CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is null)
            return NotFound();

        var row = await _historyService.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (row is null)
            return NotFound();

        var stored = await _validationService
            .GetStoredValidationAsync(id, cancellationToken)
            .ConfigureAwait(false);
        return stored is null ? NotFound() : Ok(stored);
    }

    [HttpGet("validation-report")]
    [ProducesResponseType(typeof(DepExportValidationReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetValidationReport(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var report = await _validationService
            .GetValidationReportAsync(tenantId.Value, cancellationToken)
            .ConfigureAwait(false);
        return Ok(report);
    }

    [HttpPost("history/{id:guid}/archive")]
    [ProducesResponseType(typeof(DepExportArchiveResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArchiveHistory(Guid id, CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is null)
            return NotFound();

        var row = await _historyService.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (row is null)
            return NotFound();

        var result = await _archiveService
            .ArchiveExportAsync(id, exportJson: null, cancellationToken)
            .ConfigureAwait(false);

        if (result.ErrorMessage == "Export not found")
            return NotFound();

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpGet("archive-report")]
    [ProducesResponseType(typeof(DepExportArchiveReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArchiveReport(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var report = await _archiveService
            .GetArchiveReportAsync(tenantId.Value, cancellationToken)
            .ConfigureAwait(false);
        return Ok(report);
    }

    [HttpDelete("schedule/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSchedule(Guid id, CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is null)
            return NotFound();

        var schedule = await _scheduler.GetScheduleByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (schedule is null || schedule.TenantId != _tenantAccessor.TenantId)
            return NotFound();

        await _scheduler.DeactivateScheduleAsync(id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private async Task RecordCompletedExportAsync(
        Guid tenantId,
        Guid cashRegisterId,
        DateTime fromUtc,
        DateTime toUtc,
        bool includeSpecialReceipts,
        bool includeDailyClosings,
        RksvDepExportRootDto export,
        string fileName,
        CancellationToken cancellationToken)
    {
        await _historyService.RecordCompletedAsync(
                new DepExportHistoryRecordRequest
                {
                    TenantId = tenantId,
                    CashRegisterId = cashRegisterId,
                    FromUtc = fromUtc,
                    ToUtc = toUtc,
                    ExportedByUserId = User.GetActorUserId() ?? "unknown",
                    Export = export,
                    IncludeSpecialReceipts = includeSpecialReceipts,
                    IncludeDailyClosings = includeDailyClosings,
                    FileName = fileName,
                },
                cancellationToken)
            .ConfigureAwait(false);
        // Lifecycle audit (Created / Validated / Archived) is written inside history/archive services.
    }

    private static DepExportScheduleResponse ToScheduleDto(DepExportSchedule schedule) =>
        new()
        {
            Id = schedule.Id,
            CashRegisterId = schedule.CashRegisterId,
            ScheduleType = schedule.ScheduleType,
            DayOfMonth = schedule.DayOfMonth,
            TimeOfDay = schedule.TimeOfDay,
            IsActive = schedule.IsActive,
            RecipientEmails = schedule.RecipientEmails,
            LastRunAt = schedule.LastRunAt,
            NextRunAt = schedule.NextRunAt,
            CreatedAt = schedule.CreatedAt,
        };

    private string? ResolveClientIpAddress()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string? ResolveUserAgent()
    {
        var ua = Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(ua) ? null : ua;
    }
}

public record ValidateExportRequest
{
    public string ExportJson { get; init; } = string.Empty;
}

public record TestPrueftoolRequest
{
    public string ExportJson { get; init; } = string.Empty;
}
