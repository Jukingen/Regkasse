using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>Business audit exports that are not plain <c>audit_logs</c> rows.</summary>
public enum AuditExportType
{
    Storno,
    Refund,
    VoucherRedemption,
}

public static class AuditExportTypes
{
    public const string Storno = "storno";
    public const string Refund = "refund";
    public const string VoucherRedemption = "voucher_redemption";

    public static IReadOnlyList<string> All { get; } = [Storno, Refund, VoucherRedemption];

    /// <summary>Accepts <c>voucher_redemption</c> and <c>voucher-redemption</c>, case-insensitive.</summary>
    public static bool TryParse(string? value, out AuditExportType type)
    {
        switch ((value ?? string.Empty).Trim().Replace('-', '_').ToLowerInvariant())
        {
            case Storno:
                type = AuditExportType.Storno;
                return true;
            case Refund:
                type = AuditExportType.Refund;
                return true;
            case VoucherRedemption:
                type = AuditExportType.VoucherRedemption;
                return true;
            default:
                type = default;
                return false;
        }
    }

    public static string ToWireName(AuditExportType type) => type switch
    {
        AuditExportType.Storno => Storno,
        AuditExportType.Refund => Refund,
        AuditExportType.VoucherRedemption => VoucherRedemption,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}

/// <summary>
/// Filters for the typed audit exports. Filters that do not apply to the chosen type are rejected
/// instead of silently ignored, so an auditor never reads an export that looks narrower than it is.
/// </summary>
public sealed class AuditTypedExportFilters
{
    /// <summary>Inclusive calendar start date (Austria).</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Inclusive calendar end date (Austria).</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Cashier (storno, refund) or ledger actor (voucher redemption).</summary>
    public string? UserId { get; set; }

    /// <summary>Storno and refund only.</summary>
    public Guid? CashRegisterId { get; set; }

    /// <summary>Storno only. One of <see cref="Models.StornoReason"/>.</summary>
    public string? StornoReason { get; set; }

    /// <summary>Voucher redemption only.</summary>
    public Guid? VoucherId { get; set; }

    /// <summary>Lower bound on the absolute amount.</summary>
    public decimal? MinAmount { get; set; }

    /// <summary>Upper bound on the absolute amount.</summary>
    public decimal? MaxAmount { get; set; }

    /// <summary>When both are set, export returns this page instead of the full filtered set.</summary>
    public int? Page { get; set; }

    public int? PageSize { get; set; }
}

public interface IAuditTypedExportService
{
    IReadOnlyList<string> GetColumns(AuditExportType type);

    /// <exception cref="ArgumentException">A filter is invalid or does not apply to <paramref name="type"/>.</exception>
    Task<int> CountAsync(
        AuditExportType type,
        Guid tenantId,
        AuditTypedExportFilters filters,
        CancellationToken cancellationToken = default);

    /// <exception cref="ArgumentException">The format or a filter is invalid.</exception>
    /// <exception cref="InvalidOperationException">The filtered set exceeds <see cref="IAuditExportService.MaxExportRows"/>.</exception>
    Task StreamAsync(
        AuditExportType type,
        Guid tenantId,
        AuditTypedExportFilters filters,
        string format,
        Stream output,
        CancellationToken cancellationToken = default);
}

public sealed class AuditTypedExportService : IAuditTypedExportService
{
    private const int BatchSize = 2_000;

    private static readonly string[] StornoColumns =
    [
        "Id", "ReceiptNumber", "CreatedAtUtc", "OriginalPaymentId", "OriginalReceiptNumber", "OriginalCreatedAtUtc",
        "SecondsSinceOriginal", "CashRegister", "CashierId", "PaymentMethod", "TotalAmount", "TaxAmount",
        "StornoReason", "CancellationReason", "CorrelationId",
    ];

    private static readonly string[] RefundColumns =
    [
        "Id", "ReceiptNumber", "CreatedAtUtc", "OriginalPaymentId", "OriginalReceiptNumber", "CashRegister",
        "CashierId", "PaymentMethod", "TotalAmount", "RefundAmount", "RefundReason", "RefundedAtUtc", "CorrelationId",
    ];

    private static readonly string[] VoucherRedemptionColumns =
    [
        "LedgerEntryId", "CreatedAtUtc", "VoucherId", "MaskedCode", "Currency", "RedeemedAmount", "BalanceAfter",
        "PaymentId", "ReceiptNumber", "ReceiptId", "CreatedByUserId", "CorrelationId", "IdempotencyKey",
    ];

    private readonly AppDbContext _db;

    public AuditTypedExportService(AppDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<string> GetColumns(AuditExportType type) => type switch
    {
        AuditExportType.Storno => StornoColumns,
        AuditExportType.Refund => RefundColumns,
        AuditExportType.VoucherRedemption => VoucherRedemptionColumns,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public async Task<int> CountAsync(
        AuditExportType type,
        Guid tenantId,
        AuditTypedExportFilters filters,
        CancellationToken cancellationToken = default)
    {
        Validate(type, filters);
        return type == AuditExportType.VoucherRedemption
            ? await BuildVoucherQuery(tenantId, filters).CountAsync(cancellationToken).ConfigureAwait(false)
            : await BuildPaymentQuery(type, tenantId, filters).CountAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StreamAsync(
        AuditExportType type,
        Guid tenantId,
        AuditTypedExportFilters filters,
        string format,
        Stream output,
        CancellationToken cancellationToken = default)
    {
        var normalized = (format ?? "csv").Trim().ToLowerInvariant();
        if (normalized is not ("csv" or "xlsx"))
            throw new ArgumentException("format must be csv or xlsx.", nameof(format));

        Validate(type, filters);

        var paged = filters.Page is > 0 && filters.PageSize is > 0;
        if (!paged)
        {
            var count = await CountAsync(type, tenantId, filters, cancellationToken).ConfigureAwait(false);
            if (count > IAuditExportService.MaxExportRows)
            {
                throw new InvalidOperationException(
                    $"Export exceeds maximum of {IAuditExportService.MaxExportRows} rows ({count} matched). Narrow your filters.");
            }
        }

        var cells = StreamCellsAsync(type, tenantId, filters, cancellationToken);
        if (normalized == "xlsx")
        {
            await AuditXlsxWriter.WriteAsync(output, cells, cancellationToken, sheetName: AuditExportTypes.ToWireName(type))
                .ConfigureAwait(false);
            return;
        }

        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        await foreach (var row in cells.WithCancellation(cancellationToken).ConfigureAwait(false))
            await writer.WriteLineAsync(ToCsvLine(row)).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void Validate(AuditExportType type, AuditTypedExportFilters filters)
    {
        if (filters.StartDate.HasValue && filters.EndDate.HasValue && filters.StartDate.Value.Date > filters.EndDate.Value.Date)
            throw new ArgumentException("startDate must be on or before endDate.");

        if (filters.MinAmount.HasValue && filters.MaxAmount.HasValue && filters.MinAmount > filters.MaxAmount)
            throw new ArgumentException("minAmount must be less than or equal to maxAmount.");

        if (filters.Page.HasValue != filters.PageSize.HasValue)
            throw new ArgumentException("page and pageSize must be set together.");

        var isVoucher = type == AuditExportType.VoucherRedemption;
        if (isVoucher && filters.CashRegisterId.HasValue)
            throw new ArgumentException("cashRegisterId does not apply to voucher_redemption exports.");
        if (!isVoucher && filters.VoucherId.HasValue)
            throw new ArgumentException("voucherId only applies to voucher_redemption exports.");
        if (type != AuditExportType.Storno && !string.IsNullOrWhiteSpace(filters.StornoReason))
            throw new ArgumentException("stornoReason only applies to storno exports.");

        if (!string.IsNullOrWhiteSpace(filters.StornoReason) && !TryParseStornoReason(filters.StornoReason, out _))
        {
            throw new ArgumentException(
                $"stornoReason must be one of: {string.Join(", ", ConcreteStornoReasons.Select(r => r.ToString()))}.");
        }
    }

    private static readonly StornoReason[] ConcreteStornoReasons =
        Enum.GetValues<StornoReason>().Where(r => r != StornoReason.None).ToArray();

    private static bool TryParseStornoReason(string value, out StornoReason reason) =>
        Enum.TryParse(value.Trim(), ignoreCase: true, out reason) && reason != StornoReason.None
        && Enum.IsDefined(reason);

    /// <summary>Storno and refund rows of <c>payment_details</c>, scoped to the tenant through its cash registers.</summary>
    private IQueryable<PaymentDetails> BuildPaymentQuery(AuditExportType type, Guid tenantId, AuditTypedExportFilters filters)
    {
        var query = _db.PaymentDetails.AsNoTracking()
            .ApplyTenantCashRegisterScope(_db.CashRegisters.AsNoTracking().ForResolvedTenantScope(), tenantId);

        query = type == AuditExportType.Storno
            ? query.Where(p => p.IsStorno)
            : query.Where(p => p.IsRefund && !p.IsStorno);

        var (lower, upper) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(filters.StartDate, filters.EndDate);
        if (lower.HasValue)
            query = query.Where(p => p.CreatedAt >= lower.Value);
        if (upper.HasValue)
            query = query.Where(p => p.CreatedAt < upper.Value);

        if (!string.IsNullOrWhiteSpace(filters.UserId))
        {
            var userId = filters.UserId.Trim();
            query = query.Where(p => p.CashierId == userId);
        }

        if (filters.CashRegisterId is { } registerId)
            query = query.Where(p => p.CashRegisterId == registerId);

        if (!string.IsNullOrWhiteSpace(filters.StornoReason) && TryParseStornoReason(filters.StornoReason, out var reason))
            query = query.Where(p => p.StornoReason == reason);

        if (filters.MinAmount is { } min)
            query = query.Where(p => Math.Abs(p.TotalAmount) >= min);
        if (filters.MaxAmount is { } max)
            query = query.Where(p => Math.Abs(p.TotalAmount) <= max);

        return query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id);
    }

    /// <summary>Redeem lines of the append-only voucher ledger. Never exposes the voucher code or its hash.</summary>
    private IQueryable<VoucherLedgerEntry> BuildVoucherQuery(Guid tenantId, AuditTypedExportFilters filters)
    {
        var query = _db.VoucherLedgerEntries.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.Type == VoucherTransactionType.Redeem);

        var (lower, upper) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(filters.StartDate, filters.EndDate);
        if (lower.HasValue)
            query = query.Where(e => e.CreatedAtUtc >= lower.Value);
        if (upper.HasValue)
            query = query.Where(e => e.CreatedAtUtc < upper.Value);

        if (!string.IsNullOrWhiteSpace(filters.UserId))
        {
            var userId = filters.UserId.Trim();
            query = query.Where(e => e.CreatedByUserId == userId);
        }

        if (filters.VoucherId is { } voucherId)
            query = query.Where(e => e.VoucherId == voucherId);

        if (filters.MinAmount is { } min)
            query = query.Where(e => Math.Abs(e.Amount) >= min);
        if (filters.MaxAmount is { } max)
            query = query.Where(e => Math.Abs(e.Amount) <= max);

        return query.OrderByDescending(e => e.CreatedAtUtc).ThenByDescending(e => e.Id);
    }

    private async IAsyncEnumerable<string[]> StreamCellsAsync(
        AuditExportType type,
        Guid tenantId,
        AuditTypedExportFilters filters,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return GetColumns(type).ToArray();

        // Fixed-size batches keep memory flat. Ordering is (timestamp, id) so Skip/Take pages never overlap.
        var skip = 0;
        var take = BatchSize;
        var limit = IAuditExportService.MaxExportRows;
        if (filters.Page is > 0 && filters.PageSize is > 0)
        {
            var size = Math.Clamp(filters.PageSize.Value, 1, IAuditExportService.MaxExportRows);
            skip = (filters.Page.Value - 1) * size;
            limit = size;
        }

        var emitted = 0;
        while (emitted < limit)
        {
            take = Math.Min(BatchSize, limit - emitted);
            var batch = type == AuditExportType.VoucherRedemption
                ? await LoadVoucherBatchAsync(tenantId, filters, skip, take, cancellationToken).ConfigureAwait(false)
                : await LoadPaymentBatchAsync(type, tenantId, filters, skip, take, cancellationToken).ConfigureAwait(false);
            if (batch.Count == 0)
                yield break;

            foreach (var row in batch)
                yield return row;

            emitted += batch.Count;
            skip += batch.Count;
            if (batch.Count < take)
                yield break;
        }
    }

    private async Task<List<string[]>> LoadPaymentBatchAsync(
        AuditExportType type,
        Guid tenantId,
        AuditTypedExportFilters filters,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var registers = _db.CashRegisters.AsNoTracking().ForResolvedTenantScope().Where(c => c.TenantId == tenantId);
        var rows = await BuildPaymentQuery(type, tenantId, filters)
            .Skip(skip)
            .Take(take)
            .Select(p => new PaymentRow
            {
                Id = p.Id,
                ReceiptNumber = p.ReceiptNumber,
                CreatedAt = p.CreatedAt,
                OriginalPaymentId = p.OriginalPaymentId,
                OriginalReceiptNumber = _db.PaymentDetails
                    .Where(o => o.Id == p.OriginalPaymentId)
                    .Select(o => o.ReceiptNumber)
                    .FirstOrDefault(),
                OriginalCreatedAt = _db.PaymentDetails
                    .Where(o => o.Id == p.OriginalPaymentId)
                    .Select(o => (DateTime?)o.CreatedAt)
                    .FirstOrDefault(),
                CashRegister = registers
                    .Where(c => c.Id == p.CashRegisterId)
                    .Select(c => c.RegisterNumber)
                    .FirstOrDefault(),
                CashierId = p.CashierId,
                PaymentMethodRaw = p.PaymentMethodRaw,
                TotalAmount = p.TotalAmount,
                TaxAmount = p.TaxAmount,
                StornoReason = p.StornoReason,
                CancellationReason = p.CancellationReason,
                RefundAmount = p.RefundAmount,
                RefundReason = p.RefundReason,
                RefundedAt = p.RefundedAt,
                CorrelationId = p.CorrelationId,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(r => type == AuditExportType.Storno ? ToStornoCells(r) : ToRefundCells(r)).ToList();
    }

    private async Task<List<string[]>> LoadVoucherBatchAsync(
        Guid tenantId,
        AuditTypedExportFilters filters,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var rows = await BuildVoucherQuery(tenantId, filters)
            .Skip(skip)
            .Take(take)
            .Select(e => new VoucherRow
            {
                Id = e.Id,
                CreatedAt = e.CreatedAtUtc,
                VoucherId = e.VoucherId,
                MaskedCode = e.Voucher != null ? e.Voucher.MaskedCode : null,
                Currency = e.Voucher != null ? e.Voucher.Currency : null,
                Amount = e.Amount,
                BalanceAfter = e.BalanceAfter,
                PaymentId = e.PaymentId,
                ReceiptNumber = e.Payment != null ? e.Payment.ReceiptNumber : null,
                ReceiptId = e.ReceiptId,
                CreatedByUserId = e.CreatedByUserId,
                CorrelationId = e.CorrelationId,
                IdempotencyKey = e.IdempotencyKey,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(ToVoucherCells).ToList();
    }

    private static string[] ToStornoCells(PaymentRow r) =>
    [
        r.Id.ToString(),
        Text(r.ReceiptNumber),
        Instant(r.CreatedAt),
        r.OriginalPaymentId?.ToString() ?? string.Empty,
        Text(r.OriginalReceiptNumber),
        r.OriginalCreatedAt.HasValue ? Instant(r.OriginalCreatedAt.Value) : string.Empty,
        r.OriginalCreatedAt.HasValue
            ? Math.Round((r.CreatedAt - r.OriginalCreatedAt.Value).TotalSeconds, 0).ToString(CultureInfo.InvariantCulture)
            : string.Empty,
        Text(r.CashRegister),
        Text(r.CashierId),
        PaymentMethodName(r.PaymentMethodRaw),
        Money(r.TotalAmount),
        Money(r.TaxAmount),
        r.StornoReason?.ToString() ?? string.Empty,
        Text(r.CancellationReason),
        Text(r.CorrelationId),
    ];

    private static string[] ToRefundCells(PaymentRow r) =>
    [
        r.Id.ToString(),
        Text(r.ReceiptNumber),
        Instant(r.CreatedAt),
        r.OriginalPaymentId?.ToString() ?? string.Empty,
        Text(r.OriginalReceiptNumber),
        Text(r.CashRegister),
        Text(r.CashierId),
        PaymentMethodName(r.PaymentMethodRaw),
        Money(r.TotalAmount),
        r.RefundAmount.HasValue ? Money(r.RefundAmount.Value) : string.Empty,
        Text(r.RefundReason),
        r.RefundedAt.HasValue ? Instant(r.RefundedAt.Value) : string.Empty,
        Text(r.CorrelationId),
    ];

    private static string[] ToVoucherCells(VoucherRow r) =>
    [
        r.Id.ToString(),
        Instant(r.CreatedAt),
        r.VoucherId.ToString(),
        Text(r.MaskedCode),
        Text(r.Currency),
        Money(Math.Abs(r.Amount)),
        Money(r.BalanceAfter),
        r.PaymentId?.ToString() ?? string.Empty,
        Text(r.ReceiptNumber),
        r.ReceiptId?.ToString() ?? string.Empty,
        Text(r.CreatedByUserId),
        Text(r.CorrelationId),
        Text(r.IdempotencyKey),
    ];

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Instant(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);

    private static string PaymentMethodName(string? raw) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && Enum.IsDefined(typeof(PaymentMethod), value)
            ? ((PaymentMethod)value).ToString()
            : raw ?? string.Empty;

    /// <summary>
    /// Free text from users and clients. A leading <c>= + - @</c> or control character would be run as a
    /// formula by spreadsheet tools, so such cells get a leading apostrophe.
    /// </summary>
    internal static string Text(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;
    }

    private static string ToCsvLine(string[] cells) => string.Join(',', cells.Select(CsvCell));

    private static string CsvCell(string value)
    {
        if (value.Length == 0)
            return string.Empty;
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 || value[0] == ' ' || value[^1] == ' '
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    private sealed class PaymentRow
    {
        public Guid Id { get; init; }
        public string? ReceiptNumber { get; init; }
        public DateTime CreatedAt { get; init; }
        public Guid? OriginalPaymentId { get; init; }
        public string? OriginalReceiptNumber { get; init; }
        public DateTime? OriginalCreatedAt { get; init; }
        public string? CashRegister { get; init; }
        public string? CashierId { get; init; }
        public string? PaymentMethodRaw { get; init; }
        public decimal TotalAmount { get; init; }
        public decimal TaxAmount { get; init; }
        public StornoReason? StornoReason { get; init; }
        public string? CancellationReason { get; init; }
        public decimal? RefundAmount { get; init; }
        public string? RefundReason { get; init; }
        public DateTime? RefundedAt { get; init; }
        public string? CorrelationId { get; init; }
    }

    private sealed class VoucherRow
    {
        public Guid Id { get; init; }
        public DateTime CreatedAt { get; init; }
        public Guid VoucherId { get; init; }
        public string? MaskedCode { get; init; }
        public string? Currency { get; init; }
        public decimal Amount { get; init; }
        public decimal BalanceAfter { get; init; }
        public Guid? PaymentId { get; init; }
        public string? ReceiptNumber { get; init; }
        public Guid? ReceiptId { get; init; }
        public string? CreatedByUserId { get; init; }
        public string? CorrelationId { get; init; }
        public string? IdempotencyKey { get; init; }
    }
}
