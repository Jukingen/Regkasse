using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

/// <summary>RKSV server clock sync monitoring and configuration (admin).</summary>
[Authorize]
[ApiController]
[Route("api/admin/system/time-sync")]
[Produces("application/json")]
public sealed class AdminSystemTimeSyncController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly INtpTimeSyncStatus _ntpTimeSyncStatus;
    private readonly INtpEffectiveSettingsProvider _effectiveSettings;
    private readonly IOptions<NtpSettings> _defaults;

    public AdminSystemTimeSyncController(
        AppDbContext db,
        INtpTimeSyncStatus ntpTimeSyncStatus,
        INtpEffectiveSettingsProvider effectiveSettings,
        IOptions<NtpSettings> defaults)
    {
        _db = db;
        _ntpTimeSyncStatus = ntpTimeSyncStatus;
        _effectiveSettings = effectiveSettings;
        _defaults = defaults;
    }

    /// <summary>Current persisted admin overrides (or appsettings defaults when no row exists).</summary>
    [HttpGet("configuration")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(NtpAdminConfigurationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NtpAdminConfigurationDto>> GetConfiguration(CancellationToken cancellationToken)
    {
        var row = await _db.NtpAdminSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == NtpAdminSettings.SingletonId, cancellationToken)
            .ConfigureAwait(false);
        return Ok(MapConfiguration(row));
    }

    /// <summary>Current drift snapshot plus effective configuration for the admin page.</summary>
    [HttpGet]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(AdminTimeSyncStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminTimeSyncStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        var eff = await _effectiveSettings.GetEffectiveAsync(cancellationToken).ConfigureAwait(false);
        var baseDto = _ntpTimeSyncStatus.BuildStatusDto(eff);
        var row = await _db.NtpAdminSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == NtpAdminSettings.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        var cfg = MapConfiguration(row);
        var badge = MapBadge(baseDto.WarningLevel);

        var nowUtc = DateTime.UtcNow;
        return Ok(new AdminTimeSyncStatusDto
        {
            SystemTimeUtc = nowUtc,
            SystemTimeLocalVienna = SystemTimeViennaFormatter.FormatUtcAsViennaWallClock(nowUtc),
            NtpTimeUtc = baseDto.NtpTimeUtc,
            OffsetSeconds = baseDto.OffsetSeconds,
            IsSynchronized = baseDto.IsSynchronized,
            LastSyncAt = baseDto.LastSyncAt,
            WarningLevel = baseDto.WarningLevel,
            StatusBadge = badge,
            EffectiveConfiguration = cfg
        });
    }

    /// <summary>Last 100 synchronization attempts.</summary>
    [HttpGet("logs")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(IReadOnlyList<SystemTimeSyncLogEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SystemTimeSyncLogEntryDto>>> GetLogs(CancellationToken cancellationToken)
    {
        var rows = await _db.SystemTimeSyncLogs.AsNoTracking()
            .OrderByDescending(l => l.SyncTimeUtc)
            .Take(100)
            .Select(l => new SystemTimeSyncLogEntryDto
            {
                Id = l.Id,
                SyncTimeUtc = l.SyncTimeUtc,
                OffsetSeconds = l.OffsetSeconds,
                NtpServerUsed = l.NtpServerUsed,
                IsSuccess = l.IsSuccess,
                ErrorMessage = l.ErrorMessage
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(rows);
    }

    /// <summary>Registers whose last mirrored drift exceeds the effective max offset.</summary>
    [HttpGet("drift-summary")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(TimeSyncDriftSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TimeSyncDriftSummaryDto>> GetDriftSummary(CancellationToken cancellationToken)
    {
        var eff = await _effectiveSettings.GetEffectiveAsync(cancellationToken).ConfigureAwait(false);
        var threshold = eff.MaxAllowedOffsetSeconds;

        var offenders = await _db.CashRegisters.AsNoTracking()
            .Where(r => r.LastServerTimeOffsetSeconds != null
                        && Math.Abs(r.LastServerTimeOffsetSeconds.Value) > threshold)
            .Select(r => new { r.LastServerTimeOffsetSeconds })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        double? largest = offenders.Count == 0
            ? null
            : offenders.Max(o => Math.Abs(o.LastServerTimeOffsetSeconds!.Value));

        return Ok(new TimeSyncDriftSummaryDto
        {
            HasActiveDrift = offenders.Count > 0,
            RegisterCountOverThreshold = offenders.Count,
            LargestAbsoluteOffsetSeconds = largest,
            MaxAllowedOffsetSecondsThreshold = threshold
        });
    }

    /// <summary>Persist admin overrides for auto-sync cadence and drift thresholds.</summary>
    [HttpPut("configuration")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(NtpAdminConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NtpAdminConfigurationDto>> PutConfiguration(
        [FromBody] NtpAdminConfigurationUpdateDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.SyncIntervalMinutes < 1 || dto.SyncIntervalMinutes > 24 * 60)
            return BadRequest("SyncIntervalMinutes must be between 1 and 1440.");

        if (dto.MaxAllowedOffsetSeconds < 1 || dto.MaxAllowedOffsetSeconds > 3600)
            return BadRequest("MaxAllowedOffsetSeconds must be between 1 and 3600.");

        if (dto.CriticalOffsetSeconds < 1 || dto.CriticalOffsetSeconds > 86400)
            return BadRequest("CriticalOffsetSeconds must be between 1 and 86400.");

        if (dto.CriticalOffsetSeconds < dto.MaxAllowedOffsetSeconds)
            return BadRequest("CriticalOffsetSeconds must be greater than or equal to MaxAllowedOffsetSeconds.");

        var now = DateTime.UtcNow;
        var row = await _db.NtpAdminSettings.FirstOrDefaultAsync(x => x.Id == NtpAdminSettings.SingletonId, cancellationToken)
                  .ConfigureAwait(false);

        if (row == null)
        {
            row = new NtpAdminSettings
            {
                Id = NtpAdminSettings.SingletonId,
                AutoSyncEnabled = dto.AutoSyncEnabled,
                SyncIntervalMinutes = dto.SyncIntervalMinutes,
                MaxAllowedOffsetSeconds = dto.MaxAllowedOffsetSeconds,
                CriticalOffsetSeconds = dto.CriticalOffsetSeconds,
                UpdatedAtUtc = now
            };
            _db.NtpAdminSettings.Add(row);
        }
        else
        {
            row.AutoSyncEnabled = dto.AutoSyncEnabled;
            row.SyncIntervalMinutes = dto.SyncIntervalMinutes;
            row.MaxAllowedOffsetSeconds = dto.MaxAllowedOffsetSeconds;
            row.CriticalOffsetSeconds = dto.CriticalOffsetSeconds;
            row.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var mapped = await _db.NtpAdminSettings.AsNoTracking()
            .FirstAsync(x => x.Id == NtpAdminSettings.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        return Ok(MapConfiguration(mapped));
    }

    private NtpAdminConfigurationDto MapConfiguration(NtpAdminSettings? row)
    {
        var d = _defaults.Value;
        if (row == null)
        {
            return new NtpAdminConfigurationDto
            {
                AutoSyncEnabled = d.Enabled,
                SyncIntervalMinutes = d.SyncIntervalMinutes,
                MaxAllowedOffsetSeconds = d.MaxAllowedOffsetSeconds,
                CriticalOffsetSeconds = d.CriticalOffsetSeconds,
                HasDatabaseOverride = false,
                UpdatedAtUtc = null
            };
        }

        return new NtpAdminConfigurationDto
        {
            AutoSyncEnabled = row.AutoSyncEnabled,
            SyncIntervalMinutes = row.SyncIntervalMinutes,
            MaxAllowedOffsetSeconds = row.MaxAllowedOffsetSeconds,
            CriticalOffsetSeconds = row.CriticalOffsetSeconds,
            HasDatabaseOverride = true,
            UpdatedAtUtc = row.UpdatedAtUtc
        };
    }

    private static string MapBadge(string warningLevel)
    {
        if (string.Equals(warningLevel, "critical", StringComparison.OrdinalIgnoreCase))
            return "Critical";
        if (string.Equals(warningLevel, "warning", StringComparison.OrdinalIgnoreCase))
            return "Warning";
        return "Synchronized";
    }
}
