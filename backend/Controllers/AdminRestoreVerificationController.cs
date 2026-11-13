using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.RestoreVerification;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Restore verification drill: enqueue and read results. Work runs in <see cref="RestoreVerificationOrchestratorHostedService"/> (not on HTTP thread).
/// Distinct from backup artifact verification under <see cref="AdminBackupController"/>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/restore-verification")]
[Produces("application/json")]
public sealed class AdminRestoreVerificationController : ControllerBase
{
    private readonly IRestoreVerificationManualTriggerService _trigger;
    private readonly IRestoreVerificationRunQueryService _query;
    private readonly IRestoreVerificationOperationalReadiness _readiness;
    private readonly IRestoreProofMilestonesQueryService _milestones;
    private readonly IRestoreVerificationReportService _reports;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminRestoreVerificationController(
        IRestoreVerificationManualTriggerService trigger,
        IRestoreVerificationRunQueryService query,
        IRestoreVerificationOperationalReadiness readiness,
        IRestoreProofMilestonesQueryService milestones,
        IRestoreVerificationReportService reports,
        ICurrentTenantAccessor tenantAccessor)
    {
        _trigger = trigger;
        _query = query;
        _readiness = readiness;
        _milestones = milestones;
        _reports = reports;
        _tenantAccessor = tenantAccessor;
    }

    /// <summary>Worker / dağıtık kilit yapılandırma özeti (backup artifact health değil).</summary>
    [HttpGet("readiness")]
    [HasPermission(AppPermissions.SettingsView)]
    public ActionResult<RestoreVerificationReadinessResponseDto> GetReadiness()
    {
        var snap = _readiness.GetConfigurationHealth();
        return Ok(RestoreVerificationReadinessMapper.ToDto(snap));
    }

    /// <summary>
    /// Restore drill sıraya alır (pg_restore --list + isteğe bağlı fiscal SQL + isteğe bağlı live integrity).
    /// Gövdede isteğe bağlı <c>idempotencyKey</c>: aynı anahtar mevcut satırı döndürür. Aktif Queued/Running satırı varsa yeni sıra oluşturulmaz.
    /// Yanıtta <c>runId</c>, <c>orchestrationState</c>, <c>newQueuedRunCreated</c>, <c>existingRunReturned</c> ve tam <c>run</c> bulunur.
    /// </summary>
    [HttpPost("trigger")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(RestoreVerificationTriggerResponseDto), 202)]
    public async Task<ActionResult<RestoreVerificationTriggerResponseDto>> Trigger(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RestoreVerificationManualTriggerRequestDto? body,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        var correlationId = HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
        try
        {
            var result = await _trigger.EnqueueManualAsync(
                userId,
                correlationId,
                body?.IdempotencyKey,
                sourceBackupRunId: null,
                cancellationToken);
            var dto = RestoreVerificationRunMapper.ToTriggerResponseDto(result);
            return AcceptedAtAction(nameof(GetById), new { id = result.Run.Id }, dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "RESTORE_VERIFICATION_TRIGGER_VALIDATION", error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                code = "RESTORE_VERIFICATION_ENQUEUE_CONTENTION",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Son yedek çalıştırması, pg_dump başarısı, artifact, restore drill denemesi / başarısı ve L4/L5 &quot;son bilinen iyi&quot; kilometre taşları (tek sorgu; muhafazakâr semantik).
    /// </summary>
    [HttpGet("milestones")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<RestoreProofMilestonesResponseDto>> GetProofMilestones(CancellationToken cancellationToken)
    {
        var dto = await _milestones.GetMilestonesAsync(cancellationToken);
        return Ok(dto);
    }

    [HttpGet("runs/latest")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<RestoreVerificationRunResponseDto?>> GetLatest(CancellationToken cancellationToken)
    {
        if (ManagerMissingTenant())
            return NotFound();
        var run = await _query.GetLatestAsync(BuildAccess(), cancellationToken);
        return run == null ? Ok(null) : Ok(RestoreVerificationRunMapper.ToDto(run));
    }

    [HttpGet("runs")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<RestoreVerificationHistoryResponseDto>> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] RestoreVerificationStatus? status = null,
        [FromQuery] RestoreVerificationTriggerSource? triggerSource = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? sourceBackupRunId = null,
        CancellationToken cancellationToken = default)
    {
        if (ManagerMissingTenant())
            return NotFound();

        var filter = new RestoreVerificationHistoryFilter
        {
            Status = status,
            TriggerSource = triggerSource,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            SourceBackupRunIdSearch = sourceBackupRunId
        };

        var (items, total) = await _query.GetHistoryAsync(page, pageSize, filter, BuildAccess(), cancellationToken);
        return Ok(new RestoreVerificationHistoryResponseDto
        {
            Items = items.Select(RestoreVerificationRunMapper.ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    [HttpGet("runs/{id:guid}")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<RestoreVerificationRunResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (ManagerMissingTenant())
            return NotFound();
        var run = await _query.GetByIdAsync(id, BuildAccess(), cancellationToken);
        if (run == null)
            return NotFound();
        return Ok(RestoreVerificationRunMapper.ToDto(run));
    }

    /// <summary>
    /// Audit export: <c>format=json</c> (default), <c>csv</c>, or <c>pdf</c>.
    /// System dump drills stay Super Admin only (Manager → 404).
    /// </summary>
    [HttpGet("runs/{id:guid}/report")]
    [HasPermission(AppPermissions.SettingsView)]
    [Produces("application/json", "text/csv", "application/pdf")]
    public async Task<IActionResult> GetReport(
        Guid id,
        [FromQuery] string format = "json",
        CancellationToken cancellationToken = default)
    {
        if (ManagerMissingTenant())
            return NotFound();

        var report = await _reports.GetReportAsync(id, BuildAccess(), cancellationToken);
        if (report is null)
            return NotFound();

        var kind = (format ?? "json").Trim().ToLowerInvariant();
        if (kind is "csv")
        {
            var bytes = _reports.ToCsv(report);
            return File(bytes, "text/csv; charset=utf-8", $"restore-verification-{id:D}.csv");
        }

        if (kind is "pdf")
        {
            var bytes = _reports.ToPdf(report);
            return File(bytes, "application/pdf", $"restore-verification-{id:D}.pdf");
        }

        return Ok(report);
    }

    /// <summary>
    /// Son drill için kalıcı <c>evidence_json</c> (schemaVersion, aşamalar, geçerlilik bantları). Boşsa 404 değil 200 + null gövde yok; boş dize dönebilir.
    /// </summary>
    [HttpGet("runs/{id:guid}/evidence")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<IActionResult> GetEvidenceJson(Guid id, CancellationToken cancellationToken)
    {
        if (ManagerMissingTenant())
            return NotFound();
        var run = await _query.GetByIdAsync(id, BuildAccess(), cancellationToken);
        if (run == null)
            return NotFound();
        if (string.IsNullOrEmpty(run.EvidenceJson))
            return Content("{}", "application/json");
        return Content(run.EvidenceJson, "application/json");
    }

    private RestoreVerificationAccessScope BuildAccess() =>
        new(User.IsInRole(Roles.SuperAdmin), _tenantAccessor.TenantId);

    private bool ManagerMissingTenant() =>
        !User.IsInRole(Roles.SuperAdmin) && !_tenantAccessor.TenantId.HasValue;
}
