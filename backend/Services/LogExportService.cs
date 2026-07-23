using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public sealed class LogExportResult
{
    public required byte[] Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
    public int RowCount { get; init; }
}

public interface ILogExportService
{
    const int MaxExportRows = 50_000;

    /// <summary>
    /// Export Elmah application error logs.
    /// Filename: <c>log_{tenantSlug}_{stamp}.{txt|csv|json}</c>.
    /// </summary>
    Task<LogExportResult> ExportAsync(
        string applicationName,
        string? tenantSlug,
        string format = "txt",
        int? maxRows = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Error / application log export (Elmah) with canonical download names.</summary>
public sealed class LogExportService : ILogExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private readonly IElmahErrorQueryService _elmah;
    private readonly IFileNamingService _fileNaming;
    private readonly ILogger<LogExportService> _logger;

    public LogExportService(
        IElmahErrorQueryService elmah,
        IFileNamingService fileNaming,
        ILogger<LogExportService> logger)
    {
        _elmah = elmah;
        _fileNaming = fileNaming;
        _logger = logger;
    }

    public async Task<LogExportResult> ExportAsync(
        string applicationName,
        string? tenantSlug,
        string format = "txt",
        int? maxRows = null,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(maxRows ?? ILogExportService.MaxExportRows, 1, ILogExportService.MaxExportRows);
        var rows = await _elmah
            .ListForExportAsync(applicationName, limit, cancellationToken)
            .ConfigureAwait(false);

        var normalized = LogExportFileNames.NormalizeExtension(format);
        var fileName = _fileNaming.GenerateFileName(
            LogExportFileNames.Prefix,
            normalized,
            tenantSlug: tenantSlug);

        byte[] content = normalized switch
        {
            "json" => JsonSerializer.SerializeToUtf8Bytes(rows, JsonOptions),
            "csv" => Encoding.UTF8.GetBytes(BuildCsv(rows)),
            _ => Encoding.UTF8.GetBytes(BuildTxt(rows)),
        };

        _logger.LogInformation(
            "Log export created. Application={Application}, Rows={Rows}, Format={Format}, FileName={FileName}",
            applicationName,
            rows.Count,
            normalized,
            fileName);

        return new LogExportResult
        {
            Content = content,
            ContentType = LogExportFileNames.ContentTypeForFormat(normalized),
            FileName = fileName,
            RowCount = rows.Count,
        };
    }

    private static string BuildTxt(IReadOnlyList<ElmahErrorListItemDto> rows)
    {
        var sb = new StringBuilder();
        foreach (var r in rows)
        {
            sb.Append(r.TimeUtc.ToString("o", CultureInfo.InvariantCulture)).Append('\t');
            sb.Append(r.StatusCode.ToString(CultureInfo.InvariantCulture)).Append('\t');
            sb.Append(r.Type).Append('\t');
            sb.Append(r.Host).Append('\t');
            sb.Append(r.User ?? "").Append('\t');
            sb.AppendLine(r.Message.Replace('\r', ' ').Replace('\n', ' '));
        }

        return sb.ToString();
    }

    private static string BuildCsv(IReadOnlyList<ElmahErrorListItemDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', new[]
        {
            "ErrorId", "TimeUtc", "StatusCode", "Type", "Source", "Host", "User", "Message", "Application",
        }));

        foreach (var r in rows)
        {
            sb.Append(Esc(r.ErrorId.ToString())).Append(',');
            sb.Append(Esc(r.TimeUtc.ToString("o", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.StatusCode.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.Type)).Append(',');
            sb.Append(Esc(r.Source)).Append(',');
            sb.Append(Esc(r.Host)).Append(',');
            sb.Append(Esc(r.User)).Append(',');
            sb.Append(Esc(r.Message)).Append(',');
            sb.Append(Esc(r.Application));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string Esc(string? v)
    {
        if (string.IsNullOrEmpty(v))
            return "";
        var s = v.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{s}\"";
    }
}
