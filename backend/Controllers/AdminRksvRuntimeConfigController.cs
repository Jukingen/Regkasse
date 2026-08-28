using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Constants;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin RKSV runtime overlay (Demo/Production labels + Development TSE health bypass).
/// Persisted in <c>rksv_runtime_config</c>. Does not switch hardware TSE (<c>Tse:TseMode</c> / provider).
/// Overlay <c>TseMode=Real</c> disables Development TSE health bypass.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/rksv/config")]
[Produces("application/json")]
[HasPermission(AppPermissions.SystemCritical)]
public sealed class AdminRksvRuntimeConfigController : ControllerBase
{
    private readonly IRksvRuntimeConfigService _runtimeConfig;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly IDevelopmentModeService _developmentModeService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;
    private readonly IActivityEventPublisher _activity;
    private readonly ILogger<AdminRksvRuntimeConfigController> _logger;

    public AdminRksvRuntimeConfigController(
        IRksvRuntimeConfigService runtimeConfig,
        IHostEnvironment hostEnvironment,
        IConfiguration configuration,
        IOptionsMonitor<TseOptions> tseOptions,
        IDevelopmentModeService developmentModeService,
        UserManager<ApplicationUser> userManager,
        IAuditLogService auditLogService,
        IActivityEventPublisher activity,
        ILogger<AdminRksvRuntimeConfigController> logger)
    {
        _runtimeConfig = runtimeConfig;
        _hostEnvironment = hostEnvironment;
        _configuration = configuration;
        _tseOptions = tseOptions;
        _developmentModeService = developmentModeService;
        _userManager = userManager;
        _auditLogService = auditLogService;
        _activity = activity;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AdminRksvRuntimeConfigResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminRksvRuntimeConfigResponseDto>> GetConfig(
        CancellationToken cancellationToken)
    {
        var snapshot = await _runtimeConfig.GetEffectiveAsync(cancellationToken).ConfigureAwait(false);
        return Ok(await MapAsync(snapshot, cancellationToken).ConfigureAwait(false));
    }

    [HttpPost]
    [ProducesResponseType(typeof(AdminRksvRuntimeConfigResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminRksvRuntimeConfigResponseDto>> PostConfig(
        [FromBody] AdminRksvRuntimeConfigPostRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (!User.HasPermissionClaim(AppPermissions.SystemCritical))
            return Forbid();

        if (body is null)
            return BadRequest(new { code = "BODY_REQUIRED", message = "Request body is required." });

        var actorUserId = User.GetActorUserId() ?? "unknown";
        var actorRole = User.GetActorRole() ?? "Unknown";
        var correlationId = HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
        Guid? updaterGuid = Guid.TryParse(actorUserId, out var parsed) ? parsed : null;

        var before = await _runtimeConfig.GetEffectiveAsync(cancellationToken).ConfigureAwait(false);

        RksvRuntimeSnapshot after;
        try
        {
            after = await _runtimeConfig.UpdateAsync(
                    new RksvRuntimeConfig
                    {
                        Mode = body.Mode,
                        TseMode = body.TseMode,
                        FinanzOnlineMode = body.FinanzOnlineMode,
                        ShowDemoLabel = body.ShowDemoLabel,
                        BypassTseInDevelopment = body.BypassTseInDevelopment,
                    },
                    updaterGuid,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RksvRuntimeConfigValidationException ex)
        {
            return BadRequest(new { code = RksvRuntimeConfigValidationException.ErrorCode, message = ex.Message });
        }
        catch (RksvRuntimeConfigLockException ex)
        {
            return Conflict(new
            {
                code = RksvRuntimeConfigLockException.ErrorCode,
                message = ex.Message,
                reasons = ex.Reasons,
            });
        }

        try
        {
            await _auditLogService.LogSystemOperationAsync(
                    action: "RKSV_RUNTIME_CONFIG_UPDATED",
                    entityType: "RksvRuntimeConfig",
                    userId: actorUserId,
                    userRole: actorRole,
                    description: "RKSV runtime overlay was updated (Demo/Production presentation).",
                    notes: string.IsNullOrWhiteSpace(body.Reason) ? null : body.Reason.Trim(),
                    status: AuditLogStatus.Success,
                    requestData: new { previous = ToAudit(before), requested = body, reason = body.Reason },
                    responseData: new { current = ToAudit(after) },
                    correlationIdOverride: correlationId,
                    actionType: AuditEventType.RksvRuntimeConfigChanged,
                    tenantId: SystemTenantIds.Platform,
                    oldValues: ToAudit(before),
                    newValues: ToAudit(after),
                    entityName: "rksv_runtime_config")
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RKSV runtime config: audit log write failed after successful update.");
        }

        try
        {
            await _activity.TryPublishAsync(
                    SystemTenantIds.Platform,
                    ActivityEventType.RksvRuntimeConfigChanged,
                    metadata: new
                    {
                        ActorId = actorUserId,
                        ActorRole = actorRole,
                        PreviousMode = before.Mode,
                        Mode = after.Mode,
                        TseMode = after.TseMode,
                        FinanzOnlineMode = after.FinanzOnlineMode,
                        ShowDemoLabel = after.ShowDemoLabel,
                        BypassTseInDevelopment = after.BypassTseInDevelopment,
                        Reason = body.Reason,
                    },
                    actorUserId: actorUserId,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RKSV runtime config: activity publish failed after successful update.");
        }

        _logger.LogInformation(
            "RKSV runtime config updated by {ActorUserId} ({ActorRole}). Mode={Mode} ShowDemoLabel={ShowDemoLabel}.",
            actorUserId,
            actorRole,
            after.Mode,
            after.ShowDemoLabel);

        return Ok(await MapAsync(after, cancellationToken).ConfigureAwait(false));
    }

    private static object ToAudit(RksvRuntimeSnapshot s) =>
        new
        {
            s.Mode,
            s.TseMode,
            s.FinanzOnlineMode,
            s.ShowDemoLabel,
            s.BypassTseInDevelopment,
            s.Source,
            s.OverlayPersisted,
            s.UpdatedAtUtc,
            s.UpdatedByUserId,
        };

    private async Task<AdminRksvRuntimeConfigResponseDto> MapAsync(
        RksvRuntimeSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var overlay = new TseFiscalConfigLockEvaluator.RksvLockOverlay(
            snapshot.Mode,
            snapshot.TseMode,
            snapshot.IsFinanzOnlineSimulation);
        var eval = TseFiscalConfigLockEvaluator.Evaluate(
            _hostEnvironment,
            _configuration,
            _tseOptions.CurrentValue,
            overlay);

        var fallback = RksvRuntimeConfigEnsure.SeedFromConfiguration(_configuration, _hostEnvironment);
        var updatedBy = await ResolveUpdatedByEmailAsync(snapshot.UpdatedByUserId, cancellationToken)
            .ConfigureAwait(false);

        return new AdminRksvRuntimeConfigResponseDto
        {
            Mode = snapshot.Mode,
            TseMode = snapshot.TseMode,
            FinanzOnlineMode = snapshot.FinanzOnlineMode,
            ShowDemoLabel = snapshot.ShowDemoLabel,
            BypassTseInDevelopment = snapshot.BypassTseInDevelopment,
            TseHealthBypassEffective = _developmentModeService.ShouldBypassTseCheck(),
            TseHealthBypassBlockedByRealTseMode =
                _hostEnvironment.IsDevelopment() && !snapshot.IsTseSimulation,
            Source = snapshot.Source,
            OverlayPersisted = snapshot.OverlayPersisted,
            HostEnvironment = _hostEnvironment.EnvironmentName,
            ProductionLockApplies = eval.LockApplies,
            ProductionLockOk = eval.Ok,
            ProductionLockReasons = eval.Reasons,
            RestartRequired = false,
            CanSetDemoOnThisHost = !eval.LockApplies || _tseOptions.CurrentValue.AllowUnsafeFiscalModesInProduction,
            UpdatedAtUtc = snapshot.UpdatedAtUtc,
            UpdatedBy = updatedBy,
            AppsettingsFallback = new AdminRksvRuntimeConfigValuesDto
            {
                Mode = fallback.Mode,
                TseMode = fallback.TseMode,
                FinanzOnlineMode = fallback.FinanzOnlineMode,
                ShowDemoLabel = fallback.ShowDemoLabel,
                BypassTseInDevelopment = fallback.BypassTseInDevelopment,
            },
        };
    }

    private async Task<string?> ResolveUpdatedByEmailAsync(Guid? updatedByUserId, CancellationToken cancellationToken)
    {
        if (updatedByUserId is null)
            return null;

        try
        {
            var user = await _userManager.FindByIdAsync(updatedByUserId.Value.ToString()).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return user?.Email;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RKSV runtime config: could not resolve updater {UserId}.", updatedByUserId);
            return null;
        }
    }
}
