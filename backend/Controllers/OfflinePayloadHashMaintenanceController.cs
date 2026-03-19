using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Analyze and repair legacy payload_hash vs runtime canonical SHA-256. Repair requires system.critical.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/offline-payload-hash")]
public class OfflinePayloadHashMaintenanceController : ControllerBase
{
    private readonly IOfflinePayloadHashMaintenanceService _maintenance;
    private readonly ILogger<OfflinePayloadHashMaintenanceController> _logger;
    private readonly PayloadHashGuardOptions _guardOptions;

    public OfflinePayloadHashMaintenanceController(
        IOfflinePayloadHashMaintenanceService maintenance,
        ILogger<OfflinePayloadHashMaintenanceController> logger,
        IOptions<PayloadHashGuardOptions>? guardOptions = null)
    {
        _maintenance = maintenance;
        _logger = logger;
        _guardOptions = guardOptions?.Value ?? new PayloadHashGuardOptions();
    }

    /// <summary>
    /// GET: Quick risk check — sample recent rows and return LegacyDataQualityRiskHigh and mismatch ratio for ops/UI.
    /// </summary>
    [HttpGet("risk")]
    [HasPermission(AppPermissions.ReportExport)]
    public async Task<ActionResult<OfflinePayloadHashRiskResponse>> GetRisk(CancellationToken cancellationToken)
    {
        var result = await _maintenance.AnalyzeAsync(_guardOptions.SampleSizeForExportCheck, null, cancellationToken).ConfigureAwait(false);
        if (result.LegacyDataQualityRiskHigh && result.WarningMessage != null)
            _logger.LogWarning("Offline payload_hash risk endpoint: {WarningMessage}", result.WarningMessage);
        return Ok(new OfflinePayloadHashRiskResponse
        {
            LegacyDataQualityRiskHigh = result.LegacyDataQualityRiskHigh,
            MismatchRatioPercent = result.MismatchRatioPercent,
            Scanned = result.Scanned,
            RuntimeMismatchCount = result.RuntimeMismatchCount,
            WarningMessage = result.WarningMessage
        });
    }

    public sealed class AnalyzeRequest
    {
        [Range(1, 100_000)]
        public int MaxRows { get; set; } = 10_000;

        public Guid? CashRegisterId { get; set; }
    }

    public sealed class RepairRequest
    {
        [Range(1, 100_000)]
        public int MaxRows { get; set; } = 10_000;

        public Guid? CashRegisterId { get; set; }

        /// <summary>When true, only counts rows that would be updated.</summary>
        public bool DryRun { get; set; } = true;
    }

    /// <summary>
    /// Scan recent offline rows: count where stored payload_hash != runtime canonical hash.
    /// </summary>
    [HttpPost("analyze")]
    [HasPermission(AppPermissions.ReportExport)]
    public async Task<ActionResult<OfflinePayloadHashAnalyzeResult>> Analyze(
        [FromBody] AnalyzeRequest? body,
        CancellationToken cancellationToken)
    {
        var req = body ?? new AnalyzeRequest();
        var result = await _maintenance.AnalyzeAsync(
                req.MaxRows,
                req.CashRegisterId,
                cancellationToken)
            .ConfigureAwait(false);
        if (result.LegacyDataQualityRiskHigh && result.WarningMessage != null)
            _logger.LogWarning("Offline payload_hash analyze: {WarningMessage}", result.WarningMessage);
        return Ok(result);
    }

    /// <summary>
    /// Align payload_hash to runtime canonical value where unique (CashRegisterId, payload_hash) allows.
    /// Use DryRun first; set DryRun=false only after review (requires system.critical).
    /// </summary>
    [HttpPost("repair")]
    [HasPermission(AppPermissions.SystemCritical)]
    public async Task<ActionResult<OfflinePayloadHashRepairResult>> Repair(
        [FromBody] RepairRequest? body,
        CancellationToken cancellationToken)
    {
        var req = body ?? new RepairRequest();
        var result = await _maintenance.RepairAsync(
                req.MaxRows,
                req.DryRun,
                req.CashRegisterId,
                cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }
}

public sealed class OfflinePayloadHashRiskResponse
{
    public bool LegacyDataQualityRiskHigh { get; set; }
    public double MismatchRatioPercent { get; set; }
    public int Scanned { get; set; }
    public int RuntimeMismatchCount { get; set; }
    public string? WarningMessage { get; set; }
}
