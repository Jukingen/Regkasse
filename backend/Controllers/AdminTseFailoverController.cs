using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE failover dashboard: device health, active failovers, history, manual override.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/failover")]
[Produces("application/json")]
public sealed class AdminTseFailoverController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITseFailoverService _failover;
    private readonly ITseHealthTrendService _healthTrend;
    private readonly ITsePerformanceService _performance;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;

    public AdminTseFailoverController(
        AppDbContext db,
        ITseFailoverService failover,
        ITseHealthTrendService healthTrend,
        ITsePerformanceService performance,
        IOptionsMonitor<TseOptions> tseOptions)
    {
        _db = db;
        _failover = failover;
        _healthTrend = healthTrend;
        _performance = performance;
        _tseOptions = tseOptions;
    }

    /// <summary>All TSE devices with failover / health columns (cross-tenant Super Admin).</summary>
    [HttpGet("devices")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<TseFailoverDeviceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TseFailoverDeviceDto>>> ListDevices(
        CancellationToken cancellationToken)
    {
        var devices = await _db.TseDevices
            .AsNoTracking()
            .OrderByDescending(d => d.IsFailoverActive)
            .ThenByDescending(d => d.IsPrimary)
            .ThenByDescending(d => d.IsActive)
            .ThenBy(d => d.SerialNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var enriched = await EnrichDevicesAsync(devices, cancellationToken).ConfigureAwait(false);
        return Ok(enriched);
    }

    /// <summary>Summary counters + currently active failover pairings.</summary>
    [HttpGet("status")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseFailoverStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseFailoverStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        var devices = await _db.TseDevices
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var activeBackups = devices.Where(d => d.IsFailoverActive && d.IsBackup && d.IsActive).ToList();
        var primaryMap = devices
            .Where(d => d.IsPrimary)
            .ToDictionary(d => d.Id);

        var tenantIds = activeBackups
            .Select(d => d.TenantId)
            .Concat(activeBackups
                .Select(d => d.PrimaryDeviceId)
                .Where(id => id.HasValue)
                .Select(id => primaryMap.TryGetValue(id!.Value, out var p) ? p.TenantId : null))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var tenants = await _db.Tenants
            .AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name })
            .ToDictionaryAsync(t => t.Id, cancellationToken)
            .ConfigureAwait(false);

        var activeFailovers = activeBackups.Select(b =>
        {
            primaryMap.TryGetValue(b.PrimaryDeviceId ?? Guid.Empty, out var primary);
            var tenantId = b.TenantId ?? primary?.TenantId;
            string? tenantName = null;
            if (tenantId is { } tid && tenants.TryGetValue(tid, out var tenant))
                tenantName = tenant.Name;

            return new TseActiveFailoverDto
            {
                Id = b.Id,
                PrimaryDeviceId = b.PrimaryDeviceId ?? Guid.Empty,
                PrimarySerialNumber = primary?.SerialNumber,
                BackupDeviceId = b.Id,
                BackupSerialNumber = b.SerialNumber,
                TenantId = tenantId,
                TenantName = tenantName,
                LastFailoverAt = primary?.LastFailoverAt ?? b.LastFailoverAt,
                LastFailoverReason = primary?.LastFailoverReason ?? b.LastFailoverReason,
            };
        }).Where(f => f.PrimaryDeviceId != Guid.Empty).ToList();

        return Ok(new TseFailoverStatusDto
        {
            ActiveFailoverCount = activeFailovers.Count,
            HealthyDeviceCount = devices.Count(d => d.HealthStatus == TseHealthStatus.Healthy && d.IsActive),
            ActiveDeviceCount = devices.Count(d => d.IsActive),
            BackupAvailableCount = devices.Count(d => d.IsBackup && d.IsActive && !d.IsFailoverActive),
            AutoFailoverEnabled = _tseOptions.CurrentValue.AutoFailoverEnabled,
            ActiveFailovers = activeFailovers,
        });
    }

    /// <summary>Recent failover audit rows (newest first).</summary>
    [HttpGet("history")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<TseFailoverHistoryItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TseFailoverHistoryItemDto>>> GetHistory(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);

        var rows = await _db.TseFailoverLogs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderByDescending(l => l.StartedAt)
            .Take(take)
            .Select(l => new TseFailoverHistoryItemDto
            {
                Id = l.Id,
                TenantId = l.TenantId,
                PrimaryDeviceId = l.PrimaryDeviceId,
                BackupDeviceId = l.BackupDeviceId,
                FailoverType = l.FailoverType,
                TriggerReason = l.TriggerReason,
                PreviousStatus = l.PreviousStatus,
                NewStatus = l.NewStatus,
                IsSuccessful = l.IsSuccessful,
                ErrorMessage = l.ErrorMessage,
                StartedAt = l.StartedAt,
                CompletedAt = l.CompletedAt,
                PerformedBy = l.PerformedBy,
                Notes = l.Notes,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(rows);
    }

    /// <summary>Tenant fleet health report (live device scores + alerts + recommendations).</summary>
    [HttpGet("health-report")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseHealthReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseHealthReportDto>> GetHealthReport(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        var exists = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            return NotFound();

        var report = await _healthTrend
            .GenerateHealthReportAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);
        return Ok(report);
    }

    /// <summary>Historical health score samples for trend charts.</summary>
    [HttpGet("health-trend")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<TseHealthTrendPointDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TseHealthTrendPointDto>>> GetHealthTrend(
        [FromQuery] Guid tenantId,
        [FromQuery] int days = 7,
        [FromQuery] Guid? deviceId = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        var points = await _healthTrend
            .GetHealthTrendAsync(tenantId, days, deviceId, cancellationToken)
            .ConfigureAwait(false);
        return Ok(points);
    }

    /// <summary>Probe latency / success metrics from health samples for a TSE device.</summary>
    [HttpGet("performance")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TsePerformanceMetricsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TsePerformanceMetricsDto>> GetPerformance(
        [FromQuery] Guid deviceId,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (deviceId == Guid.Empty)
            return BadRequest(new { error = "deviceId is required." });

        var to = toUtc ?? DateTime.UtcNow;
        var from = fromUtc ?? to.AddDays(-7);

        try
        {
            var metrics = await _performance
                .GetPerformanceMetricsAsync(deviceId, from, to, cancellationToken)
                .ConfigureAwait(false);
            return Ok(metrics);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Evaluate recent performance anomalies (slow / high error rate) for a device.</summary>
    [HttpGet("performance-anomalies")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TsePerformanceAlertDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TsePerformanceAlertDto>> CheckPerformanceAnomalies(
        [FromQuery] Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        if (deviceId == Guid.Empty)
            return BadRequest(new { error = "deviceId is required." });

        var alert = await _performance
            .CheckPerformanceAnomaliesAsync(deviceId, cancellationToken)
            .ConfigureAwait(false);
        return Ok(alert);
    }

    /// <summary>Super Admin forced failover to a linked backup.</summary>
    [HttpPost("manual")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseFailoverActionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TseFailoverActionResponseDto>> ManualFailover(
        [FromBody] ManualTseFailoverRequestDto body,
        CancellationToken cancellationToken)
    {
        if (body.PrimaryDeviceId == Guid.Empty || body.BackupDeviceId == Guid.Empty)
        {
            return BadRequest(new TseFailoverActionResponseDto
            {
                Success = false,
                Message = "primaryDeviceId and backupDeviceId are required.",
            });
        }

        var actorId = User.GetActorUserId() ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var result = await _failover
            .ManualFailoverAsync(body.PrimaryDeviceId, body.BackupDeviceId, actorId, cancellationToken)
            .ConfigureAwait(false);

        var dto = ToActionResponse(result);
        return result.Succeeded ? Ok(dto) : BadRequest(dto);
    }

    /// <summary>Revert signing role to the primary TSE device.</summary>
    [HttpPost("revert")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseFailoverActionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TseFailoverActionResponseDto>> Revert(
        [FromBody] RevertTseFailoverRequestDto body,
        CancellationToken cancellationToken)
    {
        if (body.PrimaryDeviceId == Guid.Empty)
        {
            return BadRequest(new TseFailoverActionResponseDto
            {
                Success = false,
                Message = "primaryDeviceId is required.",
            });
        }

        var actorId = User.GetActorUserId() ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var result = await _failover
            .RevertToPrimaryAsync(body.PrimaryDeviceId, actorId, cancellationToken)
            .ConfigureAwait(false);

        var dto = ToActionResponse(result);
        return result.Succeeded ? Ok(dto) : BadRequest(dto);
    }

    private static TseFailoverActionResponseDto ToActionResponse(FailoverResult result) =>
        new()
        {
            Success = result.Succeeded,
            Message = result.Message,
            FailoverType = result.FailoverType,
            PrimaryDeviceId = result.PrimaryDeviceId,
            BackupDeviceId = result.BackupDeviceId,
            LogId = result.LogId,
            NeedsAttention = result.NeedsAttention,
        };

    private async Task<IReadOnlyList<TseFailoverDeviceDto>> EnrichDevicesAsync(
        IReadOnlyList<TseDevice> devices,
        CancellationToken cancellationToken)
    {
        if (devices.Count == 0)
            return Array.Empty<TseFailoverDeviceDto>();

        var registerIds = devices
            .Select(d => d.CashRegisterId ?? (d.KassenId == Guid.Empty ? (Guid?)null : d.KassenId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var registers = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => registerIds.Contains(r.Id))
            .Select(r => new { r.Id, r.RegisterNumber, r.TenantId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var tenantIds = devices
            .Where(d => d.TenantId.HasValue)
            .Select(d => d.TenantId!.Value)
            .Concat(registers.Select(r => r.TenantId))
            .Distinct()
            .ToList();

        var tenants = await _db.Tenants
            .AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name, t.Slug })
            .ToDictionaryAsync(t => t.Id, cancellationToken)
            .ConfigureAwait(false);

        var registerMap = registers.ToDictionary(r => r.Id);

        return devices.Select(d =>
        {
            var registerId = d.CashRegisterId ?? (d.KassenId == Guid.Empty ? null : d.KassenId);
            registerMap.TryGetValue(registerId ?? Guid.Empty, out var reg);
            var tenantId = d.TenantId ?? reg?.TenantId;
            string? tenantName = null;
            string? tenantSlug = null;
            if (tenantId is { } tid && tenants.TryGetValue(tid, out var tenant))
            {
                tenantName = tenant.Name;
                tenantSlug = tenant.Slug;
            }

            return new TseFailoverDeviceDto
            {
                Id = d.Id,
                DeviceId = d.DeviceId,
                SerialNumber = d.SerialNumber,
                Provider = d.Provider,
                DeviceType = d.DeviceType,
                TenantId = tenantId,
                TenantName = tenantName,
                TenantSlug = tenantSlug,
                CashRegisterId = registerId,
                CashRegisterNumber = reg?.RegisterNumber,
                IsPrimary = d.IsPrimary,
                IsBackup = d.IsBackup,
                IsActive = d.IsActive,
                IsFailoverActive = d.IsFailoverActive,
                PrimaryDeviceId = d.PrimaryDeviceId,
                HealthStatus = d.HealthStatus.ToString(),
                HealthScore = d.HealthScore,
                HealthMessage = d.HealthMessage,
                LastHealthCheck = d.LastHealthCheck,
                FailoverCount = d.FailoverCount,
                LastFailoverAt = d.LastFailoverAt,
                LastFailoverReason = d.LastFailoverReason,
            };
        }).ToList();
    }
}
