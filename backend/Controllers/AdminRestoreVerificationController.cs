using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.RestoreVerification;

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

    public AdminRestoreVerificationController(
        IRestoreVerificationManualTriggerService trigger,
        IRestoreVerificationRunQueryService query,
        IRestoreVerificationOperationalReadiness readiness)
    {
        _trigger = trigger;
        _query = query;
        _readiness = readiness;
    }

    /// <summary>Worker / dağıtık kilit yapılandırma özeti (backup artifact health değil).</summary>
    [HttpGet("readiness")]
    [HasPermission(AppPermissions.SettingsView)]
    public ActionResult<RestoreVerificationReadinessResponseDto> GetReadiness()
    {
        var snap = _readiness.GetConfigurationHealth();
        return Ok(RestoreVerificationReadinessMapper.ToDto(snap));
    }

    /// <summary>Enqueue a restore drill (logical dump pg_restore --list + optional fiscal SQL + optional live integrity).</summary>
    [HttpPost("trigger")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(RestoreVerificationRunResponseDto), 202)]
    public async Task<ActionResult<RestoreVerificationRunResponseDto>> Trigger(
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        var correlationId = HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
        var run = await _trigger.EnqueueManualAsync(userId, correlationId, cancellationToken);
        var dto = RestoreVerificationRunMapper.ToDto(run);
        return AcceptedAtAction(nameof(GetById), new { id = run.Id }, dto);
    }

    [HttpGet("runs/latest")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<RestoreVerificationRunResponseDto?>> GetLatest(CancellationToken cancellationToken)
    {
        var run = await _query.GetLatestAsync(cancellationToken);
        return run == null ? Ok(null) : Ok(RestoreVerificationRunMapper.ToDto(run));
    }

    [HttpGet("runs")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<RestoreVerificationHistoryResponseDto>> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _query.GetHistoryAsync(page, pageSize, cancellationToken);
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
        var run = await _query.GetByIdAsync(id, cancellationToken);
        if (run == null)
            return NotFound();
        return Ok(RestoreVerificationRunMapper.ToDto(run));
    }
}
