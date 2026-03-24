using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Time;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Observability: DeviceId/ClientSequence coverage of replayed offline intents.
/// Supports rollout risk visibility, threshold-based alerting, and register risk scoring.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/offline-intent-coverage")]
[Produces("application/json")]
public class OfflineIntentCoverageController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<OfflineIntentCoverageController> _logger;
    private readonly CoverageGuardOptions _guardOptions;
    private readonly IAuditLogService? _auditLogService;

    public OfflineIntentCoverageController(
        AppDbContext context,
        ILogger<OfflineIntentCoverageController> logger,
        Microsoft.Extensions.Options.IOptions<CoverageGuardOptions> guardOptions,
        IAuditLogService? auditLogService = null)
    {
        _context = context;
        _logger = logger;
        _guardOptions = guardOptions?.Value ?? new CoverageGuardOptions();
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// GET: Coverage summary for replayed offline intents in a UTC window.
    /// Optional cashRegisterId filters to one register; otherwise aggregates over all registers with samples in range.
    /// </summary>
    /// <param name="fromUtc">Inclusive start (UTC). Default: 24h ago.</param>
    /// <param name="toUtc">Inclusive end (UTC). Default: now.</param>
    /// <param name="cashRegisterId">Optional: restrict to one cash register.</param>
    [HttpGet]
    [HasPermission(AppPermissions.ReportExport)]
    public async Task<ActionResult<OfflineIntentCoverageResponse>> GetCoverage(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] Guid? cashRegisterId = null,
        CancellationToken cancellationToken = default)
    {
        var to = PostgreSqlUtcDateTime.ToUtcForNpgsql(toUtc ?? DateTime.UtcNow);
        var from = PostgreSqlUtcDateTime.ToUtcForNpgsql(fromUtc ?? to.AddDays(-1));

        if (from > to)
        {
            return BadRequest(new { message = "fromUtc must be <= toUtc.", code = "INVALID_RANGE" });
        }

        try
        {
            var query = _context.OfflineIntentCoverageSamples
                .AsNoTracking()
                .Where(s => s.CreatedAtUtc >= from && s.CreatedAtUtc <= to);

            if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
                query = query.Where(s => s.CashRegisterId == cashRegisterId.Value);

            var list = await query
                .Select(s => new { s.CashRegisterId, s.HasDeviceId, s.HasClientSequence })
                .ToListAsync(cancellationToken);

            var total = list.Count;
            var withDeviceId = list.Count(s => s.HasDeviceId);
            var withSequence = list.Count(s => s.HasClientSequence);

            var deviceIdMissingRate = total > 0 ? (double)(total - withDeviceId) / total : 0;
            var sequenceMissingRate = total > 0 ? (double)(total - withSequence) / total : 0;
            var deviceIdCoveragePercent = total > 0 ? 100.0 * withDeviceId / total : 100.0;
            var sequenceCoveragePercent = total > 0 ? 100.0 * withSequence / total : 100.0;

            var byRegister = list
                .GroupBy(s => s.CashRegisterId)
                .Select(g =>
                {
                    var regTotal = g.Count();
                    var regDevice = g.Count(s => s.HasDeviceId);
                    var regSeq = g.Count(s => s.HasClientSequence);
                    var regDeviceMissing = regTotal > 0 ? (double)(regTotal - regDevice) / regTotal : 0;
                    var regSeqMissing = regTotal > 0 ? (double)(regTotal - regSeq) / regTotal : 0;
                    var riskScore = regDeviceMissing + regSeqMissing;
                    return new OfflineIntentCoverageByRegisterDto
                    {
                        CashRegisterId = g.Key,
                        Total = regTotal,
                        WithDeviceId = regDevice,
                        WithSequence = regSeq,
                        DeviceIdMissingRate = regDeviceMissing,
                        SequenceMissingRate = regSeqMissing,
                        RiskScore = Math.Round(riskScore, 4)
                    };
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            var thresholdPercent = _guardOptions.LowCoverageThresholdPercent;
            var minSamples = _guardOptions.MinSamplesForAlert;
            var lowCoverageAlert = total >= minSamples && (deviceIdCoveragePercent < thresholdPercent || sequenceCoveragePercent < thresholdPercent);
            var alertReason = lowCoverageAlert
                ? $"DeviceId coverage {deviceIdCoveragePercent:F1}% or Sequence coverage {sequenceCoveragePercent:F1}% below threshold {thresholdPercent}% (samples={total})"
                : null;

            if (lowCoverageAlert)
            {
                _logger.LogWarning(
                    "Offline intent coverage below threshold: DeviceId={DeviceIdCoverage:F1}%, Sequence={SequenceCoverage:F1}%, threshold={Threshold}%, totalSamples={Total}",
                    deviceIdCoveragePercent, sequenceCoveragePercent, thresholdPercent, total);
                if (_guardOptions.WriteAlertToAuditLog && _auditLogService != null)
                {
                    var userId = User.GetActorUserId() ?? "system";
                    var userRole = User.GetActorRole() ?? "Admin";
                    _ = _auditLogService.LogSystemOperationAsync(
                        "OfflineCoverageLow",
                        "OfflineIntentCoverage",
                        userId,
                        userRole,
                        description: alertReason,
                        status: AuditLogStatus.Success,
                        requestData: new { from, to, total, withDeviceId, withSequence, deviceIdCoveragePercent, sequenceCoveragePercent, thresholdPercent }).ConfigureAwait(false);
                }
            }

            var response = new OfflineIntentCoverageResponse
            {
                FromUtc = from,
                ToUtc = to,
                Total = total,
                WithDeviceId = withDeviceId,
                WithSequence = withSequence,
                DeviceIdMissingRate = deviceIdMissingRate,
                SequenceMissingRate = sequenceMissingRate,
                DeviceIdCoveragePercent = deviceIdCoveragePercent,
                SequenceCoveragePercent = sequenceCoveragePercent,
                LowCoverageAlert = lowCoverageAlert,
                AlertReason = alertReason,
                ByRegister = byRegister
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Offline intent coverage query failed");
            return StatusCode(500, new { message = "Internal server error while retrieving coverage.", code = "COVERAGE_QUERY_ERROR" });
        }
    }

    /// <summary>
    /// GET: Top N riskiest registers by coverage risk score (DeviceId missing rate + Sequence missing rate) in the given UTC window.
    /// </summary>
    /// <param name="fromUtc">Inclusive start (UTC). Default: 24h ago.</param>
    /// <param name="toUtc">Inclusive end (UTC). Default: now.</param>
    /// <param name="limit">Max number of registers to return. Default 10.</param>
    /// <param name="cashRegisterId">Optional: restrict top-risk ranking to one cash register.</param>
    [HttpGet("top-risk")]
    [HasPermission(AppPermissions.ReportExport)]
    public async Task<ActionResult<OfflineIntentCoverageTopRiskResponse>> GetTopRisk(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int limit = 10,
        [FromQuery] Guid? cashRegisterId = null,
        CancellationToken cancellationToken = default)
    {
        var to = PostgreSqlUtcDateTime.ToUtcForNpgsql(toUtc ?? DateTime.UtcNow);
        var from = PostgreSqlUtcDateTime.ToUtcForNpgsql(fromUtc ?? to.AddDays(-1));
        if (from > to)
            return BadRequest(new { message = "fromUtc must be <= toUtc.", code = "INVALID_RANGE" });
        limit = Math.Clamp(limit, 1, 100);

        try
        {
            var query = _context.OfflineIntentCoverageSamples
                .AsNoTracking()
                .Where(s => s.CreatedAtUtc >= from && s.CreatedAtUtc <= to);

            if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
                query = query.Where(s => s.CashRegisterId == cashRegisterId.Value);

            var list = await query
                .Select(s => new { s.CashRegisterId, s.HasDeviceId, s.HasClientSequence })
                .ToListAsync(cancellationToken);

            var byRegister = list
                .GroupBy(s => s.CashRegisterId)
                .Select(g =>
                {
                    var regTotal = g.Count();
                    var regDevice = g.Count(s => s.HasDeviceId);
                    var regSeq = g.Count(s => s.HasClientSequence);
                    var regDeviceMissing = regTotal > 0 ? (double)(regTotal - regDevice) / regTotal : 0;
                    var regSeqMissing = regTotal > 0 ? (double)(regTotal - regSeq) / regTotal : 0;
                    return new OfflineIntentCoverageByRegisterDto
                    {
                        CashRegisterId = g.Key,
                        Total = regTotal,
                        WithDeviceId = regDevice,
                        WithSequence = regSeq,
                        DeviceIdMissingRate = regDeviceMissing,
                        SequenceMissingRate = regSeqMissing,
                        RiskScore = Math.Round(regDeviceMissing + regSeqMissing, 4)
                    };
                })
                .OrderByDescending(x => x.RiskScore)
                .ThenByDescending(x => x.Total)
                .Take(limit)
                .ToList();

            return Ok(new OfflineIntentCoverageTopRiskResponse
            {
                FromUtc = from,
                ToUtc = to,
                Limit = limit,
                Registers = byRegister
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Offline intent coverage top-risk query failed");
            return StatusCode(500, new { message = "Internal server error while retrieving top-risk registers.", code = "COVERAGE_TOP_RISK_ERROR" });
        }
    }
}

/// <summary>Response for GET api/admin/offline-intent-coverage.</summary>
public class OfflineIntentCoverageResponse
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public int Total { get; set; }
    public int WithDeviceId { get; set; }
    public int WithSequence { get; set; }
    /// <summary>Fraction of intents missing DeviceId (0..1).</summary>
    public double DeviceIdMissingRate { get; set; }
    /// <summary>Fraction of intents missing ClientSequenceNumber (0..1).</summary>
    public double SequenceMissingRate { get; set; }
    /// <summary>DeviceId coverage as percent (0..100).</summary>
    public double DeviceIdCoveragePercent { get; set; }
    /// <summary>Sequence coverage as percent (0..100).</summary>
    public double SequenceCoveragePercent { get; set; }
    /// <summary>True when coverage is below configured threshold (actionable alert).</summary>
    public bool LowCoverageAlert { get; set; }
    /// <summary>Reason for LowCoverageAlert when set.</summary>
    public string? AlertReason { get; set; }
    public IReadOnlyList<OfflineIntentCoverageByRegisterDto> ByRegister { get; set; } = Array.Empty<OfflineIntentCoverageByRegisterDto>();
}

/// <summary>Response for GET api/admin/offline-intent-coverage/top-risk.</summary>
public class OfflineIntentCoverageTopRiskResponse
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public int Limit { get; set; }
    public IReadOnlyList<OfflineIntentCoverageByRegisterDto> Registers { get; set; } = Array.Empty<OfflineIntentCoverageByRegisterDto>();
}

public class OfflineIntentCoverageByRegisterDto
{
    public Guid CashRegisterId { get; set; }
    public int Total { get; set; }
    public int WithDeviceId { get; set; }
    public int WithSequence { get; set; }
    /// <summary>Fraction of intents missing DeviceId for this register (0..1).</summary>
    public double DeviceIdMissingRate { get; set; }
    /// <summary>Fraction of intents missing ClientSequence for this register (0..1).</summary>
    public double SequenceMissingRate { get; set; }
    /// <summary>Risk score: DeviceIdMissingRate + SequenceMissingRate (0..2). Higher = riskier.</summary>
    public double RiskScore { get; set; }
}
