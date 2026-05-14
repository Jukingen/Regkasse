using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Frontend-admin: read/update development-mode singleton settings. Gated by <see cref="AppPermissions.SystemCritical"/>
/// (catalog: SuperAdmin-only; other roles do not receive <c>system.critical</c>).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/development-mode")]
[Produces("application/json")]
[HasPermission(AppPermissions.SystemCritical)]
public sealed class AdminDevelopmentModeController : ControllerBase
{
    private readonly IDevelopmentModeService _developmentModeService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AdminDevelopmentModeController> _logger;

    public AdminDevelopmentModeController(
        IDevelopmentModeService developmentModeService,
        UserManager<ApplicationUser> userManager,
        IAuditLogService auditLogService,
        ILogger<AdminDevelopmentModeController> logger)
    {
        _developmentModeService = developmentModeService;
        _userManager = userManager;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>Load singleton development-mode settings from the database (cached in <see cref="DevelopmentModeService"/>).</summary>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(DevelopmentModeSettingsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DevelopmentModeSettingsResponseDto>> GetSettings(CancellationToken cancellationToken)
    {
        DevelopmentModeSettings row;
        try
        {
            row = await _developmentModeService.GetSettingsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin development-mode: failed to load settings.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Ok(await MapToResponseDtoAsync(row, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Persist development-mode singleton; reloads service cache and writes an audit row.</summary>
    [HttpPut("settings")]
    [ProducesResponseType(typeof(DevelopmentModeSettingsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DevelopmentModeSettingsResponseDto>> PutSettings(
        [FromBody] DevelopmentModeSettingsPutRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (!User.HasPermissionClaim(AppPermissions.SystemCritical))
            return Forbid();

        if (body is null)
            return BadRequest(new { code = "BODY_REQUIRED", message = "Request body is required." });

        var actorUserId = User.GetActorUserId() ?? "unknown";
        var actorRole = User.GetActorRole() ?? "Unknown";
        var correlationId = HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
        Guid? updaterGuid = null;
        if (!string.IsNullOrWhiteSpace(actorUserId) && Guid.TryParse(actorUserId, out var parsedGuid))
            updaterGuid = parsedGuid;

        DevelopmentModeSettings beforeRow;
        try
        {
            beforeRow = await _developmentModeService.GetSettingsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin development-mode: failed to load settings before update.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var oldAudit = ToAuditSnapshot(beforeRow);

        var entity = new DevelopmentModeSettings
        {
            Id = DevelopmentModeSettings.SingletonId,
            Enabled = body.Enabled,
            BypassLicense = body.BypassLicense,
            BypassNtpCheck = body.BypassNtpCheck,
            BypassTseCheck = body.BypassTseCheck,
            SimulateOffline = body.SimulateOffline,
            ForceOnline = body.ForceOnline,
            ValidDays = body.ValidDays,
            Features = body.Features ?? [],
        };

        try
        {
            await _developmentModeService.UpdateSettingsAsync(entity, updaterGuid).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Admin development-mode: update rejected (host environment or validation).");
            return BadRequest(new { code = "DEVELOPMENT_MODE_UPDATE_NOT_ALLOWED", message = ex.Message });
        }

        await _developmentModeService.ReloadSettingsCacheAsync(cancellationToken).ConfigureAwait(false);

        DevelopmentModeSettings afterRow;
        try
        {
            afterRow = await _developmentModeService.GetSettingsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin development-mode: failed to load settings after update.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var newAudit = ToAuditSnapshot(afterRow);

        try
        {
            await _auditLogService.LogSystemOperationAsync(
                    action: "DEVELOPMENT_MODE_SETTINGS_UPDATED",
                    entityType: "DevelopmentModeSettings",
                    userId: actorUserId,
                    userRole: actorRole,
                    description: "Development mode singleton settings were updated.",
                    notes: null,
                    status: AuditLogStatus.Success,
                    errorDetails: null,
                    requestData: new { previous = oldAudit, requested = ToAuditRequestSnapshot(body) },
                    responseData: new { current = newAudit },
                    correlationIdOverride: correlationId)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Admin development-mode: audit log write failed after successful update.");
        }

        _logger.LogInformation(
            "Development mode settings updated by user {ActorUserId} ({ActorRole}). Enabled={Enabled}, BypassLicense={BypassLicense}.",
            actorUserId,
            actorRole,
            afterRow.Enabled,
            afterRow.BypassLicense);

        return Ok(await MapToResponseDtoAsync(afterRow, cancellationToken).ConfigureAwait(false));
    }

    private static object ToAuditRequestSnapshot(DevelopmentModeSettingsPutRequestDto body) =>
        new
        {
            body.Enabled,
            body.BypassLicense,
            body.BypassNtpCheck,
            body.BypassTseCheck,
            body.SimulateOffline,
            body.ForceOnline,
            body.ValidDays,
            Features = body.Features ?? [],
        };

    private static object ToAuditSnapshot(DevelopmentModeSettings s) =>
        new
        {
            s.Enabled,
            s.BypassLicense,
            s.BypassNtpCheck,
            s.BypassTseCheck,
            s.SimulateOffline,
            s.ForceOnline,
            s.ValidDays,
            Features = s.Features ?? [],
            s.UpdatedAtUtc,
            s.UpdatedByUserId,
        };

    private async Task<DevelopmentModeSettingsResponseDto> MapToResponseDtoAsync(
        DevelopmentModeSettings row,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var updatedBy = await ResolveUpdatedByEmailAsync(row.UpdatedByUserId, cancellationToken).ConfigureAwait(false);
        return new DevelopmentModeSettingsResponseDto
        {
            Enabled = row.Enabled,
            BypassLicense = row.BypassLicense,
            BypassNtpCheck = row.BypassNtpCheck,
            BypassTseCheck = row.BypassTseCheck,
            SimulateOffline = row.SimulateOffline,
            ForceOnline = row.ForceOnline,
            ValidDays = row.ValidDays,
            Features = row.Features ?? [],
            UpdatedAtUtc = DateTime.SpecifyKind(row.UpdatedAtUtc, DateTimeKind.Utc),
            UpdatedBy = updatedBy,
        };
    }

    private async Task<string?> ResolveUpdatedByEmailAsync(Guid? updatedByUserId, CancellationToken cancellationToken)
    {
        if (updatedByUserId is null)
            return null;

        var id = updatedByUserId.Value.ToString();
        try
        {
            var user = await _userManager.FindByIdAsync(id).ConfigureAwait(false);
            return user?.Email;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Admin development-mode: could not resolve updater for user id {UserId}.", id);
            return null;
        }
    }
}
