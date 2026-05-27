using System.Globalization;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IAuditExportService
{
    const int MaxExportRows = 100_000;
    const int BackgroundExportThreshold = 5_000;

    Task<int> CountForExportAsync(AuditLogQueryFilters filters, CancellationToken cancellationToken = default);

    Task StreamExportAsync(
        AuditLogQueryFilters filters,
        string format,
        Stream output,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportToBytesAsync(
        AuditLogQueryFilters filters,
        string format,
        CancellationToken cancellationToken = default);
}

public sealed class AuditExportService : IAuditExportService
{
    private const int BatchSize = 2_000;
    private readonly AppDbContext _context;
    private readonly ILogger<AuditExportService> _logger;

    public AuditExportService(AppDbContext context, ILogger<AuditExportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> CountForExportAsync(AuditLogQueryFilters filters, CancellationToken cancellationToken = default)
    {
        var count = await BuildQuery(filters).CountAsync(cancellationToken).ConfigureAwait(false);
        return count;
    }

    public async Task StreamExportAsync(
        AuditLogQueryFilters filters,
        string format,
        Stream output,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeFormat(format);
        var count = await CountForExportAsync(filters, cancellationToken).ConfigureAwait(false);
        if (count > IAuditExportService.MaxExportRows)
            throw new InvalidOperationException($"Export exceeds maximum of {IAuditExportService.MaxExportRows} rows ({count} matched). Narrow your filters.");

        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: normalized == "excel"), leaveOpen: true);

        if (normalized == "json")
        {
            await writer.WriteAsync("[").ConfigureAwait(false);
            var first = true;
            await foreach (var log in StreamRowsAsync(filters, cancellationToken).ConfigureAwait(false))
            {
                if (!first) await writer.WriteAsync(",").ConfigureAwait(false);
                first = false;
                await writer.WriteAsync(JsonSerializer.Serialize(log, JsonExportOptions)).ConfigureAwait(false);
            }
            await writer.WriteAsync("]").ConfigureAwait(false);
            return;
        }

        var delimiter = normalized == "excel" ? ';' : ',';
        await writer.WriteLineAsync(CsvHeader(delimiter)).ConfigureAwait(false);
        await foreach (var log in StreamRowsAsync(filters, cancellationToken).ConfigureAwait(false))
        {
            await writer.WriteLineAsync(ToCsvRow(log, delimiter)).ConfigureAwait(false);
        }
    }

    public async Task<byte[]> ExportToBytesAsync(
        AuditLogQueryFilters filters,
        string format,
        CancellationToken cancellationToken = default)
    {
        await using var ms = new MemoryStream();
        await StreamExportAsync(filters, format, ms, cancellationToken).ConfigureAwait(false);
        return ms.ToArray();
    }

    private IQueryable<AuditLog> BuildQuery(AuditLogQueryFilters filters) =>
        _context.AuditLogs.AsNoTracking().ApplyFilters(filters).OrderByDescending(a => a.Timestamp);

    private async IAsyncEnumerable<AuditLog> StreamRowsAsync(
        AuditLogQueryFilters filters,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var skip = 0;
        while (true)
        {
            var batch = await BuildQuery(filters)
                .Skip(skip)
                .Take(BatchSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (batch.Count == 0)
                yield break;

            foreach (var row in batch)
                yield return row;

            skip += batch.Count;
            if (skip >= IAuditExportService.MaxExportRows)
                yield break;
        }
    }

    private static string NormalizeFormat(string format)
    {
        var f = (format ?? "csv").Trim().ToLowerInvariant();
        return f switch
        {
            "json" => "json",
            "excel" or "xlsx" or "xls" => "excel",
            _ => "csv",
        };
    }

    private static readonly JsonSerializerOptions JsonExportOptions = new() { WriteIndented = false };

    private static string CsvHeader(char delimiter) =>
        string.Join(delimiter, new[]
        {
            "Id", "SessionId", "UserId", "UserRole", "Action", "EntityType", "EntityId", "EntityName",
            "Status", "Timestamp", "Description", "IpAddress", "CorrelationId", "TransactionId",
            "OldValues", "NewValues", "Changes", "RequestData", "ResponseData", "ErrorDetails",
        });

    private static string ToCsvRow(AuditLog log, char delimiter)
    {
        string Esc(string? v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            var s = v.Replace("\"", "\"\"");
            return $"\"{s}\"";
        }

        return string.Join(delimiter, new[]
        {
            Esc(log.Id.ToString()),
            Esc(log.SessionId),
            Esc(log.UserId),
            Esc(log.UserRole),
            Esc(log.Action),
            Esc(log.EntityType),
            Esc(log.EntityId?.ToString()),
            Esc(log.EntityName),
            Esc(log.Status.ToString()),
            Esc(log.Timestamp.ToString("o", CultureInfo.InvariantCulture)),
            Esc(log.Description),
            Esc(log.IpAddress),
            Esc(log.CorrelationId),
            Esc(log.TransactionId),
            Esc(log.OldValues),
            Esc(log.NewValues),
            Esc(log.Changes),
            Esc(log.RequestData),
            Esc(log.ResponseData),
            Esc(log.ErrorDetails),
        });
    }
}
