using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>RKSV operator diagnostics (clock sync).</summary>
[Authorize]
[ApiController]
[Route("api/system")]
[Produces("application/json")]
public sealed class SystemController : ControllerBase
{
    private readonly INtpTimeSyncStatus _ntpTimeSyncStatus;
    private readonly INtpEffectiveSettingsProvider _ntpEffectiveSettings;
    private readonly INtpSynchronizationCoordinator _ntpSynchronizationCoordinator;
    private readonly IDevelopmentModeService _developmentModeService;

    public SystemController(
        INtpTimeSyncStatus ntpTimeSyncStatus,
        INtpEffectiveSettingsProvider ntpEffectiveSettings,
        INtpSynchronizationCoordinator ntpSynchronizationCoordinator,
        IDevelopmentModeService developmentModeService)
    {
        _ntpTimeSyncStatus = ntpTimeSyncStatus;
        _ntpEffectiveSettings = ntpEffectiveSettings;
        _ntpSynchronizationCoordinator = ntpSynchronizationCoordinator;
        _developmentModeService = developmentModeService;
    }

    /// <summary>Public read model for persisted development-mode toggles (POS + FA badge). No authentication required.</summary>
    [HttpGet("development-mode")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(DevelopmentModeSettingsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DevelopmentModeSettingsResponseDto>> GetDevelopmentModeSettings(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var row = await _developmentModeService.GetSettingsAsync().ConfigureAwait(false);
        return Ok(new DevelopmentModeSettingsResponseDto
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
            UpdatedBy = null,
        });
    }

    /// <summary>Current system vs NTP drift snapshot for DEP readiness checks.</summary>
    [HttpGet("time/status")]
    [ProducesResponseType(typeof(SystemTimeStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemTimeStatusDto>> GetTimeStatus(CancellationToken cancellationToken)
    {
        var eff = await _ntpEffectiveSettings.GetEffectiveAsync(cancellationToken).ConfigureAwait(false);
        var dto = _ntpTimeSyncStatus.BuildStatusDto(eff);
        return Ok(dto);
    }

    /// <summary>Forces one NTP sampling cycle even when auto-sync is disabled.</summary>
    [HttpPost("time/sync")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(NtpManualSyncResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NtpManualSyncResponseDto>> PostManualSync(CancellationToken cancellationToken)
    {
        var eff = await _ntpEffectiveSettings.GetEffectiveAsync(cancellationToken).ConfigureAwait(false);
        var result = await _ntpSynchronizationCoordinator
            .RunSynchronizationCycleAsync(eff, ignoreDisabled: true, cancellationToken)
            .ConfigureAwait(false);

        var syncUtc = DateTime.UtcNow;
        return Ok(new NtpManualSyncResponseDto
        {
            Success = result.LogicalSuccess && result.Ran,
            Message = result.Message,
            OffsetSeconds = result.AverageOffsetSeconds,
            SyncTimeUtc = syncUtc
        });
    }
}
