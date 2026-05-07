using System.Globalization;
using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Compliance: audit trail for DEP/receipt fiscal export downloads (who, when, filters, heuristic size).</summary>
[Authorize]
[ApiController]
[Route("api/admin/audit")]
[Produces("application/json")]
public sealed class AdminFiscalExportAuditController : ControllerBase
{
    private readonly IFiscalExportAuditLogReader _reader;

    public AdminFiscalExportAuditController(IFiscalExportAuditLogReader reader)
    {
        _reader = reader;
    }

    /// <summary>Paged fiscal export download audit log (successful builds are logged today).</summary>
    [HttpGet("fiscal-export-logs")]
    [HasPermission(AppPermissions.AuditView)]
    [ProducesResponseType(typeof(FiscalExportAuditLogsPagedResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FiscalExportAuditLogsPagedResponseDto>> List(
        [FromQuery] DateTime? downloadFrom = null,
        [FromQuery] DateTime? downloadTo = null,
        [FromQuery] string? userSearch = null,
        [FromQuery] string exportType = FiscalExportAuditExportTypeFilter.All,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var dto = await _reader.ListAsync(downloadFrom, downloadTo, userSearch, exportType, page, pageSize, cancellationToken)
            .ConfigureAwait(false);
        return Ok(dto);
    }

    /// <summary>Full row with raw Request/Response JSON for compliance review.</summary>
    [HttpGet("fiscal-export-logs/{id:guid}")]
    [HasPermission(AppPermissions.AuditView)]
    [ProducesResponseType(typeof(FiscalExportAuditLogDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FiscalExportAuditLogDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _reader.GetDetailAsync(id, cancellationToken).ConfigureAwait(false);
        return row == null ? NotFound() : Ok(row);
    }

    /// <summary>
    /// Auditor CSV extract (same filters as list). Requires <see cref="AppPermissions.AuditExport"/>.
    /// When the filtered set exceeds maxRows the response is truncated; see <c>X-Fiscal-Audit-Export-Truncated</c>.
    /// </summary>
    [HttpGet("fiscal-export-logs/export")]
    [HasPermission(AppPermissions.AuditExport)]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] DateTime? downloadFrom = null,
        [FromQuery] DateTime? downloadTo = null,
        [FromQuery] string? userSearch = null,
        [FromQuery] string exportType = FiscalExportAuditExportTypeFilter.All,
        [FromQuery] int maxRows = 5000,
        CancellationToken cancellationToken = default)
    {
        if (maxRows < 1) maxRows = 1;
        if (maxRows > 50_000) maxRows = 50_000;

        exportType = string.IsNullOrWhiteSpace(exportType)
            ? FiscalExportAuditExportTypeFilter.All
            : exportType.Trim();

        var totalMatching = await _reader.CountMatchingAsync(downloadFrom, downloadTo, userSearch, exportType, cancellationToken)
            .ConfigureAwait(false);
        var items = await _reader.ListForCsvExportAsync(downloadFrom, downloadTo, userSearch, exportType, maxRows, cancellationToken)
            .ConfigureAwait(false);

        if (totalMatching > items.Count)
            Response.Headers.Append("X-Fiscal-Audit-Export-Truncated", "true");
        Response.Headers.Append("X-Fiscal-Audit-Export-Total-Matching", totalMatching.ToString(CultureInfo.InvariantCulture));
        Response.Headers.Append("X-Fiscal-Audit-Export-Returned", items.Count.ToString(CultureInfo.InvariantCulture));

        var fileName =
            $"fiscal-export-download-audit_{DateTime.UtcNow:yyyyMMdd_HHmmss}_UTC.csv";
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(BuildCsv(items));
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);

        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    private static string BuildCsv(IReadOnlyList<FiscalExportAuditLogListItemDto> rows)
    {
        static string C(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            var v = value.Replace("\"", "\"\"", StringComparison.Ordinal);
            return $"\"{v}\"";
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", new[]
        {
            "AuditId", "DownloadTimeUtc", "UserId", "Username", "IpAddress",
            "ExportType", "IncludesCsvFragment", "ExportPeriodFromUtc", "ExportPeriodToUtc",
            "EstimatedFileSizeBytes", "Success", "LongRangeBulkWarning",
        }));

        foreach (var r in rows)
        {
            sb.Append(string.Join(",", new[]
            {
                C(r.Id.ToString()),
                C(r.DownloadTimeUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
                C(r.UserId),
                C(r.Username),
                C(r.IpAddress),
                C(r.ExportTypeLabel),
                C(r.IncludesCsvFragment ? "true" : "false"),
                C(r.ExportPeriodFromUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
                C(r.ExportPeriodToUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
                C(r.EstimatedFileSizeBytes?.ToString(CultureInfo.InvariantCulture)),
                C(r.Success ? "true" : "false"),
                C(r.LongRangeBulkWarning ? "true" : "false"),
            }));
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
