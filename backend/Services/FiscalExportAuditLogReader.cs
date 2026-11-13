using System.Globalization;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IFiscalExportAuditLogReader
{
    Task<FiscalExportAuditLogsPagedResponseDto> ListAsync(
        DateTime? downloadFrom,
        DateTime? downloadTo,
        string? userSearch,
        string? exportType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Returns up to <paramref name="maxRows"/> rows matching the same filters as <see cref="ListAsync"/> (for auditor CSV).</summary>
    Task<IReadOnlyList<FiscalExportAuditLogListItemDto>> ListForCsvExportAsync(
        DateTime? downloadFrom,
        DateTime? downloadTo,
        string? userSearch,
        string? exportType,
        int maxRows,
        CancellationToken cancellationToken = default);

    Task<int> CountMatchingAsync(
        DateTime? downloadFrom,
        DateTime? downloadTo,
        string? userSearch,
        string? exportType,
        CancellationToken cancellationToken = default);

    Task<FiscalExportAuditLogDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);
}

internal sealed class FiscalExportAuditLogReader : IFiscalExportAuditLogReader
{
    private readonly AppDbContext _context;

    public FiscalExportAuditLogReader(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<FiscalExportAuditLogsPagedResponseDto> ListAsync(
        DateTime? downloadFrom,
        DateTime? downloadTo,
        string? userSearch,
        string? exportType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
            page = 1;
        if (pageSize < 1)
            pageSize = 25;
        if (pageSize > 100)
            pageSize = 100;

        exportType = string.IsNullOrWhiteSpace(exportType)
            ? FiscalExportAuditExportTypeFilter.All
            : exportType.Trim().ToLowerInvariant();

        var query = BuildFilteredQuery(downloadFrom, downloadTo, userSearch, exportType);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var skip = (page - 1) * pageSize;
        var rows = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip(skip)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var nameMap = await ResolveUsernamesAsync(rows, cancellationToken).ConfigureAwait(false);
        var items = rows.Select(r => MapListItem(r, nameMap)).ToList();

        return new FiscalExportAuditLogsPagedResponseDto
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize),
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FiscalExportAuditLogListItemDto>> ListForCsvExportAsync(
        DateTime? downloadFrom,
        DateTime? downloadTo,
        string? userSearch,
        string? exportType,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        if (maxRows < 1)
            maxRows = 1;
        if (maxRows > 50_000)
            maxRows = 50_000;

        exportType = string.IsNullOrWhiteSpace(exportType)
            ? FiscalExportAuditExportTypeFilter.All
            : exportType.Trim().ToLowerInvariant();

        var query = BuildFilteredQuery(downloadFrom, downloadTo, userSearch, exportType);
        var rows = await query
            .OrderByDescending(a => a.Timestamp)
            .Take(maxRows)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var nameMap = await ResolveUsernamesAsync(rows, cancellationToken).ConfigureAwait(false);
        return rows.Select(r => MapListItem(r, nameMap)).ToList();
    }

    /// <inheritdoc />
    public async Task<int> CountMatchingAsync(
        DateTime? downloadFrom,
        DateTime? downloadTo,
        string? userSearch,
        string? exportType,
        CancellationToken cancellationToken = default)
    {
        exportType = string.IsNullOrWhiteSpace(exportType)
            ? FiscalExportAuditExportTypeFilter.All
            : exportType.Trim().ToLowerInvariant();

        var query = BuildFilteredQuery(downloadFrom, downloadTo, userSearch, exportType);
        return await query.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<FiscalExportAuditLogDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _context.AuditLogs.AsNoTracking()
            .Where(a => a.Id == id && a.EntityType == AuditLogEntityTypes.FISCAL_EXPORT)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (row == null)
            return null;

        var map = await ResolveUsernamesAsync(new[] { row }, cancellationToken).ConfigureAwait(false);
        var detail = MapDetail(row, map);
        return detail;
    }

    internal IQueryable<AuditLog> BuildFilteredQuery(
        DateTime? downloadFrom,
        DateTime? downloadTo,
        string? userSearch,
        string exportType)
    {
        var query = _context.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == AuditLogEntityTypes.FISCAL_EXPORT);

        var (lo, hi) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(downloadFrom, downloadTo);
        if (lo.HasValue)
            query = query.Where(a => a.Timestamp >= lo.Value);
        if (hi.HasValue)
            query = query.Where(a => a.Timestamp < hi.Value);

        if (!string.IsNullOrWhiteSpace(userSearch))
        {
            var term = userSearch.Trim();
            if (Guid.TryParse(term, out var uid))
                query = query.Where(a => a.UserId == uid.ToString());
            else
            {
                var escaped = EscapeForILike(term);
                var pattern = $"%{escaped}%";
                query = query.Where(a =>
                    _context.Users.Any(u => u.Id == a.UserId &&
                                              u.UserName != null && EF.Functions.ILike(u.UserName, pattern))
                    ||
                    (_context.Users.Any(u => u.Id == a.UserId && u.Email != null && EF.Functions.ILike(u.Email, pattern)))
                    ||
                    (a.ActorDisplayName != null && EF.Functions.ILike(a.ActorDisplayName, pattern)));
            }
        }

        switch (exportType)
        {
            case FiscalExportAuditExportTypeFilter.Pdf:
                query = query.Where(a => a.Action.EndsWith("Pdf"));
                break;
            case FiscalExportAuditExportTypeFilter.Json:
                query = query.Where(a =>
                    a.Action.EndsWith("Json") ||
                    a.Action.EndsWith("JsonDownload"));
                break;
            case FiscalExportAuditExportTypeFilter.Csv:
                query = query.Where(a =>
                    (a.RequestData ?? string.Empty).Contains("\"includeCsv\":true", StringComparison.Ordinal)
                    || (a.RequestData ?? string.Empty).Contains("\"IncludeCsv\":true", StringComparison.Ordinal));
                break;
            default:
                break;
        }

        return query;
    }

    private async Task<IReadOnlyDictionary<string, string>> ResolveUsernamesAsync(
        IEnumerable<AuditLog> rows,
        CancellationToken cancellationToken)
    {
        var ids = rows.Select(r => r.UserId).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var users = await _context.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.Email })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var u in users)
        {
            var label = string.IsNullOrWhiteSpace(u.UserName) ? (u.Email ?? u.Id) : u.UserName;
            map[u.Id] = label;
        }

        return map;
    }

    private static string EscapeForILike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static FiscalExportAuditLogListItemDto MapListItem(
        AuditLog row,
        IReadOnlyDictionary<string, string> usernames)
    {
        FiscalExportAuditParse.TryParse(row.RequestData, row.ResponseData, row.IpAddress, out var parsed);
        var username = usernames.TryGetValue(row.UserId, out var n)
            ? n
            : (string.IsNullOrWhiteSpace(row.ActorDisplayName) ? row.UserId : row.ActorDisplayName!);

        var label = FiscalExportAuditParse.ResolveExportLabel(parsed.ExportFormat ?? string.Empty, parsed.IncludeCsv);

        var longRange = FiscalExportAuditParse.IsLongRangeBulk(parsed.FromUtc, parsed.ToUtc);

        return new FiscalExportAuditLogListItemDto
        {
            Id = row.Id,
            DownloadTimeUtc = row.Timestamp.Kind == DateTimeKind.Utc ? row.Timestamp : DateTime.SpecifyKind(row.Timestamp, DateTimeKind.Utc),
            UserId = row.UserId,
            Username = username,
            IpAddress = parsed.ClientIpFallback,
            ExportTypeLabel = label,
            IncludesCsvFragment = parsed.IncludeCsv,
            ExportPeriodFromUtc = parsed.FromUtc,
            ExportPeriodToUtc = parsed.ToUtc,
            EstimatedFileSizeBytes = parsed.EstimateBytes,
            Success = row.Status == AuditLogStatus.Success,
            LongRangeBulkWarning = longRange,
        };
    }

    private FiscalExportAuditLogDetailDto MapDetail(
        AuditLog row,
        IReadOnlyDictionary<string, string> usernames)
    {
        var core = MapListItem(row, usernames);
        return new FiscalExportAuditLogDetailDto
        {
            Id = core.Id,
            DownloadTimeUtc = core.DownloadTimeUtc,
            UserId = core.UserId,
            Username = core.Username,
            IpAddress = core.IpAddress,
            ExportTypeLabel = core.ExportTypeLabel,
            IncludesCsvFragment = core.IncludesCsvFragment,
            ExportPeriodFromUtc = core.ExportPeriodFromUtc,
            ExportPeriodToUtc = core.ExportPeriodToUtc,
            EstimatedFileSizeBytes = core.EstimatedFileSizeBytes,
            Success = core.Success,
            LongRangeBulkWarning = core.LongRangeBulkWarning,
            Action = row.Action,
            Description = row.Description,
            UserRole = row.UserRole,
            RequestDataJson = row.RequestData,
            ResponseDataJson = row.ResponseData,
            ErrorDetails = row.ErrorDetails,
        };
    }
}

internal static class FiscalExportAuditParse
{
    internal readonly struct Parsed
    {
        public DateTime? FromUtc { get; init; }
        public DateTime? ToUtc { get; init; }
        public bool IncludeCsv { get; init; }
        public string? ExportFormat { get; init; }
        public string? ClientIpFallback { get; init; }
        public int ReceiptCount { get; init; }
        public int ClosingCount { get; init; }
        public long? EstimateBytes { get; init; }
    }

    public static bool TryParse(
        string? requestJson,
        string? responseJson,
        string? rowIpAddress,
        out Parsed parsed)
    {
        parsed = default;
        DateTime? fromUtc = null;
        DateTime? toUtc = null;
        var includeCsv = false;
        string? exportFormat = null;
        string? clientIp = rowIpAddress;
        var receiptCount = 0;
        var closingCount = 0;

        if (!string.IsNullOrWhiteSpace(requestJson))
            TryReadRequest(requestJson, ref fromUtc, ref toUtc, ref includeCsv, ref exportFormat, ref clientIp);

        if (!string.IsNullOrWhiteSpace(responseJson))
            TryReadResponse(responseJson, ref receiptCount, ref closingCount);

        if (string.IsNullOrWhiteSpace(clientIp) && !string.IsNullOrWhiteSpace(requestJson))
        {
            // Legacy rows may miss HttpContext IP; tolerate missing clientIp in JSON.
            TryReadLooseIp(requestJson, ref clientIp);
        }

        var estimate = EstimatePayloadBytes(exportFormat, includeCsv, receiptCount, closingCount);

        parsed = new Parsed
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            IncludeCsv = includeCsv,
            ExportFormat = exportFormat,
            ClientIpFallback = clientIp,
            ReceiptCount = receiptCount,
            ClosingCount = closingCount,
            EstimateBytes = estimate,
        };
        return true;
    }

    private static void TryReadRequest(
        string json,
        ref DateTime? fromUtc,
        ref DateTime? toUtc,
        ref bool includeCsv,
        ref string? exportFormat,
        ref string? clientIp)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("fromUtc", out var fu))
                fromUtc = ReadDateUtc(fu);
            else if (root.TryGetProperty("FromUtc", out var fu2))
                fromUtc = ReadDateUtc(fu2);

            if (root.TryGetProperty("toUtc", out var tu))
                toUtc = ReadDateUtc(tu);
            else if (root.TryGetProperty("ToUtc", out var tu2))
                toUtc = ReadDateUtc(tu2);

            if (root.TryGetProperty("includeCsv", out var ic) && ic.ValueKind == JsonValueKind.True)
                includeCsv = true;
            else if (root.TryGetProperty("IncludeCsv", out var ic2) && ic2.ValueKind == JsonValueKind.True)
                includeCsv = true;

            if (root.TryGetProperty("exportFormat", out var ef))
                exportFormat = ef.GetString();
            else if (root.TryGetProperty("ExportFormat", out var ef2))
                exportFormat = ef2.GetString();

            if (root.TryGetProperty("clientIp", out var ci))
                clientIp = ci.GetString();
            else if (root.TryGetProperty("ClientIp", out var ci2))
                clientIp = ci2.GetString();
        }
        catch
        {
            // Malformed truncation from JsonPayloadMaxLength — keep projections best-effort.
        }
    }

    private static void TryReadResponse(string json, ref int receiptCount, ref int closingCount)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("receiptCount", out var rc))
                receiptCount = ReadInt(rc);
            else if (root.TryGetProperty("ReceiptCount", out var rc2))
                receiptCount = ReadInt(rc2);

            if (root.TryGetProperty("closingCount", out var cc))
                closingCount = ReadInt(cc);
            else if (root.TryGetProperty("ClosingCount", out var cc2))
                closingCount = ReadInt(cc2);
        }
        catch { /* truncated JSON */ }
    }

    private static void TryReadLooseIp(string json, ref string? clientIp)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("clientIp", out var ci))
                clientIp ??= ci.GetString();
            else if (root.TryGetProperty("ClientIp", out var ci2))
                clientIp ??= ci2.GetString();
        }
        catch { /* ignore */ }
    }

    private static DateTime? ReadDateUtc(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Null)
            return null;
        if (e.ValueKind != JsonValueKind.String)
            return null;
        var s = e.GetString();
        return string.IsNullOrWhiteSpace(s) ? null : DateTime.Parse(s!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    private static int ReadInt(JsonElement e) =>
        e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var i) ? i : 0;

    public static string ResolveExportLabel(string exportFormat, bool includeCsv)
    {
        var fmt = exportFormat?.Trim().ToLowerInvariant() ?? string.Empty;
        var primary = fmt switch
        {
            "pdf" => "PDF",
            "json" => "JSON",
            "jsondownload" => "JSON",
            _ => fmt.Length > 0 ? fmt.ToUpperInvariant() : "—",
        };
        return includeCsv ? $"{primary} + CSV" : primary;
    }

    public static bool IsLongRangeBulk(DateTime? fromUtc, DateTime? toUtc)
    {
        if (!fromUtc.HasValue || !toUtc.HasValue)
            return false;
        var span = toUtc.Value - fromUtc.Value;
        return span.TotalDays > 365;
    }

    private static long? EstimatePayloadBytes(string? exportFormat, bool includeCsv, int receipts, int closings)
    {
        var fmt = exportFormat?.Trim().ToLowerInvariant() ?? string.Empty;
        long estimate = fmt switch
        {
            "pdf" => 60_000L + receipts * 3_200L + closings * 800L,
            "jsondownload" or "json" => 900L + receipts * 2_600L + closings * 600L,
            _ => 1_024L + receipts * 2_000L + closings * 400L,
        };
        if (includeCsv)
            estimate += receipts * 450L + 2_048L;

        return Math.Max(estimate, 0);
    }
}
