using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
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
    /// GET: Export analyze result as CSV (conflicts + repairable). Read-only; same filters as analyze (maxRows, cashRegisterId).
    /// </summary>
    [HttpGet("export")]
    [HasPermission(AppPermissions.ReportExport)]
    public async Task<IActionResult> ExportAnalyze(
        [FromQuery] int maxRows = 10_000,
        [FromQuery] Guid? cashRegisterId = null,
        CancellationToken cancellationToken = default)
    {
        maxRows = Math.Clamp(maxRows, 1, 100_000);
        var result = await _maintenance.AnalyzeAsync(maxRows, cashRegisterId, cancellationToken).ConfigureAwait(false);
        var csv = BuildAnalyzeCsv(result);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "offline-payload-hash-analyze.csv");
    }

    private static string BuildAnalyzeCsv(OfflinePayloadHashAnalyzeResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Type,CashRegisterId,CanonicalHash,RowId,CreatedAtUtc,SkipReason,SeveritySuggestion,MismatchRowIds,OccupantRowIds,LatestCreatedAtUtc");
        foreach (var g in result.ConflictGroups)
        {
            sb.Append("Conflict,");
            sb.Append(CsvEscape(g.CashRegisterId.ToString()));
            sb.Append(',');
            sb.Append(CsvEscape(g.CanonicalHash));
            sb.Append(",,"); // RowId, CreatedAtUtc empty for conflict group
            sb.Append(',');
            sb.Append(CsvEscape(g.SkipReason));
            sb.Append(',');
            sb.Append(CsvEscape(g.SeveritySuggestion));
            sb.Append(',');
            sb.Append(CsvEscape(string.Join(";", g.MismatchRowIds)));
            sb.Append(',');
            sb.Append(CsvEscape(string.Join(";", g.OccupantRowIds)));
            sb.Append(',');
            sb.Append(g.LatestCreatedAtUtc.HasValue ? CsvEscape(g.LatestCreatedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture)) : "");
            sb.AppendLine();
        }
        foreach (var r in result.RepairableItems)
        {
            sb.Append("Repairable,");
            sb.Append(CsvEscape(r.CashRegisterId.ToString()));
            sb.Append(',');
            sb.Append(CsvEscape(r.CanonicalHash));
            sb.Append(',');
            sb.Append(CsvEscape(r.RowId.ToString()));
            sb.Append(',');
            sb.Append(r.CreatedAtUtc.HasValue ? CsvEscape(r.CreatedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture)) : "");
            sb.Append(",,,,,"); // SkipReason, SeveritySuggestion, MismatchRowIds, OccupantRowIds, LatestCreatedAtUtc empty
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        if (value.AsSpan().IndexOfAny("\",\r\n") >= 0)
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
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
