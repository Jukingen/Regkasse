using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Vouchers;

public sealed class VoucherExportResult
{
    public required byte[] Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
    public int RowCount { get; init; }
}

public interface IVoucherExportService
{
    const int MaxExportRows = 50_000;

    /// <summary>
    /// Exports tenant vouchers. Codes are redacted: only <see cref="Voucher.MaskedCode"/> is included;
    /// plaintext codes and <see cref="Voucher.CodeHash"/> are never exported.
    /// </summary>
    Task<VoucherExportResult> ExportAsync(
        Guid tenantId,
        string? tenantSlug,
        string format,
        string? statusFilter = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Admin voucher list export (JSON/CSV) with redacted codes.</summary>
public sealed class VoucherExportService : IVoucherExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private readonly AppDbContext _context;
    private readonly IFileNamingService _fileNaming;
    private readonly ILogger<VoucherExportService> _logger;

    public VoucherExportService(
        AppDbContext context,
        IFileNamingService fileNaming,
        ILogger<VoucherExportService> logger)
    {
        _context = context;
        _fileNaming = fileNaming;
        _logger = logger;
    }

    public async Task<VoucherExportResult> ExportAsync(
        Guid tenantId,
        string? tenantSlug,
        string format,
        string? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Vouchers.AsNoTracking().Where(v => v.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(statusFilter)
            && Enum.TryParse<VoucherStatus>(statusFilter.Trim(), ignoreCase: true, out var status))
        {
            query = query.Where(v => v.Status == status);
        }

        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        if (count > IVoucherExportService.MaxExportRows)
        {
            throw new InvalidOperationException(
                $"Export exceeds maximum of {IVoucherExportService.MaxExportRows} rows ({count} matched). Narrow your filters.");
        }

        // Project only safe fields — never CodeHash or any plaintext code.
        var rows = await query
            .OrderByDescending(v => v.CreatedAtUtc)
            .Take(IVoucherExportService.MaxExportRows)
            .Select(v => new VoucherExportRow
            {
                Id = v.Id,
                MaskedCode = v.MaskedCode,
                InitialAmount = v.InitialAmount,
                RemainingAmount = v.RemainingAmount,
                Currency = v.Currency,
                Status = v.Status.ToString(),
                ValidFromUtc = v.ValidFromUtc,
                ExpiresAtUtc = v.ExpiresAtUtc,
                CreatedByUserId = v.CreatedByUserId,
                CreatedAtUtc = v.CreatedAtUtc,
                CancelledAtUtc = v.CancelledAtUtc,
                CancellationReason = v.CancellationReason,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Defense-in-depth: redact any accidental full-looking code in MaskedCode.
        foreach (var row in rows)
            row.MaskedCode = RedactCodeHint(row.MaskedCode);

        var normalized = VoucherExportFileNames.NormalizeExtension(format);
        var fileName = _fileNaming.GenerateFileName(
            VoucherExportFileNames.Prefix,
            normalized,
            tenantSlug: tenantSlug);
        byte[] content = normalized == "csv"
            ? Encoding.UTF8.GetBytes(BuildCsv(rows))
            : JsonSerializer.SerializeToUtf8Bytes(rows, JsonOptions);

        _logger.LogInformation(
            "Voucher export created. TenantId={TenantId}, Rows={Rows}, Format={Format}, FileName={FileName}",
            tenantId,
            rows.Count,
            normalized,
            fileName);

        return new VoucherExportResult
        {
            Content = content,
            ContentType = VoucherExportFileNames.ContentTypeForFormat(normalized),
            FileName = fileName,
            RowCount = rows.Count,
        };
    }

    /// <summary>
    /// Ensures export never ships a full voucher code. Operator hints like <c>****1234</c> are kept;
    /// long digit/alnum strings without masking markers are replaced.
    /// </summary>
    internal static string RedactCodeHint(string? maskedCode)
    {
        if (string.IsNullOrWhiteSpace(maskedCode))
            return "***";

        var value = maskedCode.Trim();
        if (value.Contains('*', StringComparison.Ordinal) || value.Contains('•', StringComparison.Ordinal))
            return value;

        // Unmasked-looking value: keep last 4 chars at most.
        if (value.Length <= 4)
            return $"****{value}";

        return $"****{value[^4..]}";
    }

    private static string BuildCsv(IReadOnlyList<VoucherExportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', new[]
        {
            "Id", "MaskedCode", "InitialAmount", "RemainingAmount", "Currency", "Status",
            "ValidFromUtc", "ExpiresAtUtc", "CreatedByUserId", "CreatedAtUtc",
            "CancelledAtUtc", "CancellationReason",
        }));

        foreach (var r in rows)
        {
            sb.Append(Esc(r.Id.ToString())).Append(',');
            sb.Append(Esc(r.MaskedCode)).Append(',');
            sb.Append(Esc(r.InitialAmount.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.RemainingAmount.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.Currency)).Append(',');
            sb.Append(Esc(r.Status)).Append(',');
            sb.Append(Esc(r.ValidFromUtc.ToString("o", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.ExpiresAtUtc.ToString("o", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.CreatedByUserId)).Append(',');
            sb.Append(Esc(r.CreatedAtUtc.ToString("o", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.CancelledAtUtc?.ToString("o", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.CancellationReason));
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

    private sealed class VoucherExportRow
    {
        public Guid Id { get; init; }
        public string MaskedCode { get; set; } = string.Empty;
        public decimal InitialAmount { get; init; }
        public decimal RemainingAmount { get; init; }
        public string Currency { get; init; } = "EUR";
        public string Status { get; init; } = string.Empty;
        public DateTime ValidFromUtc { get; init; }
        public DateTime ExpiresAtUtc { get; init; }
        public string CreatedByUserId { get; init; } = string.Empty;
        public DateTime CreatedAtUtc { get; init; }
        public DateTime? CancelledAtUtc { get; init; }
        public string? CancellationReason { get; init; }
    }
}
