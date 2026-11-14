using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface ITenantDataExportService
{
    /// <summary>
    /// Writes one tabular export to <paramref name="output"/>.
    /// Rows are read with <c>AsAsyncEnumerable</c> and flushed per row.
    /// </summary>
    Task WriteAsync(
        Stream output,
        string type,
        string format,
        bool includeAllTenants,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Operational CSV/xlsx export for catalog, sales, orders, audit, inventory, and TSE metadata.
/// JWS payloads, signature bytes, and credentials are omitted. Rows are streamed, not buffered.
/// </summary>
public sealed class TenantDataExportService : ITenantDataExportService
{
    public const string CsvContentType = "text/csv; charset=utf-8";
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly string[] ProductHeaders =
    [
        "Id", "TenantId", "Name", "Price", "TaxRate", "Barcode", "Category", "StockQuantity", "IsActive",
    ];

    private static readonly string[] CustomerHeaders =
    [
        "Id", "TenantId", "Name", "CustomerNumber", "Email", "Phone", "IsActive",
    ];

    private static readonly string[] ReceiptHeaders =
    [
        "ReceiptId", "TenantId", "ReceiptNumber", "IssuedAt", "CashRegisterId", "PaymentId", "SubTotal", "TaxTotal", "GrandTotal",
    ];

    private static readonly string[] PaymentHeaders =
    [
        "Id", "TenantId", "CashRegisterId", "CustomerName", "TotalAmount", "TaxAmount", "PaymentMethod", "ReceiptNumber", "CreatedAt", "IsStorno",
    ];

    private static readonly string[] UserHeaders =
    [
        "UserId", "TenantId", "Email", "UserName", "FirstName", "LastName", "Role", "IsActive",
    ];

    private static readonly string[] OrderHeaders =
    [
        "Id", "Status", "Total", "CustomerName", "CreatedAt", "TenantId",
    ];

    private static readonly string[] AuditLogHeaders =
    [
        "Action", "Entity", "User", "Timestamp", "TenantId",
    ];

    private static readonly string[] InventoryHeaders =
    [
        "Product", "Stock", "ReorderLevel", "TenantId",
    ];

    private static readonly string[] TseSignatureHeaders =
    [
        "ReceiptNumber", "SignatureDate", "TseId", "TenantId",
    ];

    private readonly AppDbContext _db;
    private readonly ILogger<TenantDataExportService> _logger;

    public TenantDataExportService(AppDbContext db, ILogger<TenantDataExportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task WriteAsync(
        Stream output,
        string type,
        string format,
        bool includeAllTenants,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        var normalizedType = NormalizeType(type);
        var normalizedFormat = NormalizeFormat(format);
        var headers = HeadersFor(normalizedType);
        var rows = RowsFor(normalizedType, includeAllTenants, cancellationToken);

        _logger.LogInformation(
            "Tenant data export started. Type={ExportType} Format={ExportFormat} IncludeAllTenants={IncludeAllTenants}",
            normalizedType,
            normalizedFormat,
            includeAllTenants);

        if (normalizedFormat == "xlsx")
        {
            await StreamingXlsxWriter.WriteAsync(output, headers, rows, cancellationToken).ConfigureAwait(false);
            return;
        }

        await WriteCsvAsync(output, headers, rows, cancellationToken).ConfigureAwait(false);
    }

    public static bool TryNormalizeType(string? type, out string normalized)
    {
        normalized = (type ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "products" or "customers" or "receipts" or "payments" or "users"
            or "orders" or "audit_logs" or "inventory" or "tse_signatures";
    }

    public static bool TryNormalizeFormat(string? format, out string normalized)
    {
        normalized = (format ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "csv" or "xlsx";
    }

    public static string ContentTypeFor(string format) =>
        NormalizeFormat(format) == "xlsx" ? XlsxContentType : CsvContentType;

    public static string FileNameFor(string type, string format, DateTime utcNow) =>
        $"{NormalizeType(type)}-{utcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)}.{NormalizeFormat(format)}";

    private static string NormalizeType(string type)
    {
        if (!TryNormalizeType(type, out var normalized))
            throw new ArgumentOutOfRangeException(nameof(type), "Export type must be products, customers, receipts, payments, users, orders, audit_logs, inventory, or tse_signatures.");
        return normalized;
    }

    private static string NormalizeFormat(string format)
    {
        if (!TryNormalizeFormat(format, out var normalized))
            throw new ArgumentOutOfRangeException(nameof(format), "Export format must be csv or xlsx.");
        return normalized;
    }

    private static string[] HeadersFor(string type) => type switch
    {
        "products" => ProductHeaders,
        "customers" => CustomerHeaders,
        "receipts" => ReceiptHeaders,
        "payments" => PaymentHeaders,
        "users" => UserHeaders,
        "orders" => OrderHeaders,
        "audit_logs" => AuditLogHeaders,
        "inventory" => InventoryHeaders,
        "tse_signatures" => TseSignatureHeaders,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private IAsyncEnumerable<string[]> RowsFor(string type, bool includeAllTenants, CancellationToken cancellationToken) =>
        type switch
        {
            "products" => StreamProducts(includeAllTenants, cancellationToken),
            "customers" => StreamCustomers(includeAllTenants, cancellationToken),
            "receipts" => StreamReceipts(includeAllTenants, cancellationToken),
            "payments" => StreamPayments(includeAllTenants, cancellationToken),
            "users" => StreamUsers(includeAllTenants, cancellationToken),
            "orders" => StreamOrders(includeAllTenants, cancellationToken),
            "audit_logs" => StreamAuditLogs(includeAllTenants, cancellationToken),
            "inventory" => StreamInventory(includeAllTenants, cancellationToken),
            "tse_signatures" => StreamTseSignatures(includeAllTenants, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

    private async IAsyncEnumerable<string[]> StreamProducts(
        bool includeAllTenants,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = _db.Products.AsNoTracking();
        if (includeAllTenants)
            query = query.IgnoreQueryFilters();

        var rows = query
            .OrderBy(p => p.TenantId)
            .ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.TenantId,
                p.Name,
                p.Price,
                p.TaxRate,
                p.Barcode,
                p.Category,
                p.StockQuantity,
                p.IsActive,
            })
            .AsAsyncEnumerable();

        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return
            [
                Id(row.Id),
                Id(row.TenantId),
                row.Name,
                Dec(row.Price),
                Dec(row.TaxRate),
                row.Barcode ?? string.Empty,
                row.Category,
                row.StockQuantity.ToString(CultureInfo.InvariantCulture),
                Bool(row.IsActive),
            ];
        }
    }

    private async IAsyncEnumerable<string[]> StreamCustomers(
        bool includeAllTenants,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = _db.Customers.AsNoTracking();
        if (includeAllTenants)
            query = query.IgnoreQueryFilters();
        else
            query = query.Where(c => !c.IsSystem);

        var rows = query
            .OrderBy(c => c.TenantId)
            .ThenBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.TenantId,
                c.Name,
                c.CustomerNumber,
                c.Email,
                c.Phone,
                c.IsActive,
            })
            .AsAsyncEnumerable();

        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return
            [
                Id(row.Id),
                Id(row.TenantId),
                row.Name,
                row.CustomerNumber,
                row.Email,
                row.Phone,
                Bool(row.IsActive),
            ];
        }
    }

    private async IAsyncEnumerable<string[]> StreamReceipts(
        bool includeAllTenants,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = _db.Receipts.AsNoTracking();
        if (includeAllTenants)
            query = query.IgnoreQueryFilters();

        var rows = query
            .OrderBy(r => r.TenantId)
            .ThenBy(r => r.IssuedAt)
            .Select(r => new
            {
                r.ReceiptId,
                r.TenantId,
                r.ReceiptNumber,
                r.IssuedAt,
                r.CashRegisterId,
                r.PaymentId,
                r.SubTotal,
                r.TaxTotal,
                r.GrandTotal,
            })
            .AsAsyncEnumerable();

        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return
            [
                Id(row.ReceiptId),
                Id(row.TenantId),
                row.ReceiptNumber,
                row.IssuedAt.ToString("o", CultureInfo.InvariantCulture),
                Id(row.CashRegisterId),
                Id(row.PaymentId),
                Dec(row.SubTotal),
                Dec(row.TaxTotal),
                Dec(row.GrandTotal),
            ];
        }
    }

    private async IAsyncEnumerable<string[]> StreamPayments(
        bool includeAllTenants,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var registers = _db.CashRegisters.AsNoTracking();
        if (includeAllTenants)
            registers = registers.IgnoreQueryFilters();

        var rows =
            from payment in _db.PaymentDetails.AsNoTracking()
            join register in registers on payment.CashRegisterId equals register.Id
            orderby register.TenantId, payment.CreatedAt
            select new
            {
                payment.Id,
                register.TenantId,
                payment.CashRegisterId,
                payment.CustomerName,
                payment.TotalAmount,
                payment.TaxAmount,
                payment.PaymentMethodRaw,
                payment.ReceiptNumber,
                payment.CreatedAt,
                payment.IsStorno,
            };

        await foreach (var row in rows.AsAsyncEnumerable().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return
            [
                Id(row.Id),
                Id(row.TenantId),
                Id(row.CashRegisterId),
                row.CustomerName,
                Dec(row.TotalAmount),
                Dec(row.TaxAmount),
                PaymentMethodName(row.PaymentMethodRaw),
                row.ReceiptNumber,
                row.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
                Bool(row.IsStorno),
            ];
        }
    }

    private async IAsyncEnumerable<string[]> StreamUsers(
        bool includeAllTenants,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var memberships = _db.UserTenantMemberships.AsNoTracking().Where(m => m.IsActive);
        if (includeAllTenants)
            memberships = memberships.IgnoreQueryFilters();

        var rows =
            from membership in memberships
            join user in _db.Users.AsNoTracking() on membership.UserId equals user.Id
            orderby membership.TenantId, user.Email
            select new
            {
                user.Id,
                membership.TenantId,
                Email = user.Email ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                user.FirstName,
                user.LastName,
                user.Role,
                user.IsActive,
            };

        await foreach (var row in rows.AsAsyncEnumerable().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return
            [
                row.Id,
                Id(row.TenantId),
                row.Email,
                row.UserName,
                row.FirstName,
                row.LastName,
                row.Role,
                Bool(row.IsActive),
            ];
        }
    }

    private async IAsyncEnumerable<string[]> StreamOrders(
        bool includeAllTenants,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = _db.OnlineOrders.AsNoTracking();
        if (includeAllTenants)
            query = query.IgnoreQueryFilters();

        var rows = query
            .OrderBy(o => o.TenantId)
            .ThenBy(o => o.CreatedAt)
            .Select(o => new
            {
                o.Id,
                o.OrderStatus,
                o.Total,
                o.CustomerName,
                o.CreatedAt,
                o.TenantId,
            })
            .AsAsyncEnumerable();

        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return
            [
                Id(row.Id),
                row.OrderStatus,
                Dec(row.Total),
                row.CustomerName,
                When(row.CreatedAt),
                Id(row.TenantId),
            ];
        }
    }

    private async IAsyncEnumerable<string[]> StreamAuditLogs(
        bool includeAllTenants,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = _db.AuditLogs.AsNoTracking();
        if (includeAllTenants)
            query = query.IgnoreQueryFilters();

        var rows = query
            .OrderBy(a => a.TenantId)
            .ThenBy(a => a.Timestamp)
            .Select(a => new
            {
                a.Action,
                a.EntityType,
                a.UserId,
                a.Timestamp,
                a.TenantId,
            })
            .AsAsyncEnumerable();

        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return
            [
                row.Action,
                row.EntityType,
                row.UserId,
                When(row.Timestamp),
                Id(row.TenantId),
            ];
        }
    }

    private async IAsyncEnumerable<string[]> StreamInventory(
        bool includeAllTenants,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = _db.Products.AsNoTracking();
        if (includeAllTenants)
            query = query.IgnoreQueryFilters();

        var rows = query
            .OrderBy(p => p.TenantId)
            .ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Name,
                p.StockQuantity,
                p.MinStockLevel,
                p.TenantId,
            })
            .AsAsyncEnumerable();

        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return
            [
                row.Name,
                row.StockQuantity.ToString(CultureInfo.InvariantCulture),
                row.MinStockLevel.ToString(CultureInfo.InvariantCulture),
                Id(row.TenantId),
            ];
        }
    }

    private async IAsyncEnumerable<string[]> StreamTseSignatures(
        bool includeAllTenants,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = _db.TseSignatures.AsNoTracking();
        if (includeAllTenants)
            query = query.IgnoreQueryFilters();

        var rows = query
            .OrderBy(s => s.TenantId)
            .ThenBy(s => s.CreatedAt)
            .Select(s => new
            {
                s.InvoiceNumber,
                s.CreatedAt,
                s.TseDeviceId,
                s.TenantId,
            })
            .AsAsyncEnumerable();

        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return
            [
                row.InvoiceNumber,
                When(row.CreatedAt),
                row.TseDeviceId.HasValue ? Id(row.TseDeviceId.Value) : string.Empty,
                Id(row.TenantId),
            ];
        }
    }

    private static async Task WriteCsvAsync(
        Stream output,
        IReadOnlyList<string> headers,
        IAsyncEnumerable<string[]> rows,
        CancellationToken cancellationToken)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        await output.WriteAsync(preamble, cancellationToken).ConfigureAwait(false);

        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            NewLine = "\n",
        });

        foreach (var header in headers)
            csv.WriteField(header);
        await csv.NextRecordAsync().ConfigureAwait(false);

        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            foreach (var field in row)
                csv.WriteField(field);
            await csv.NextRecordAsync().ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string Id(Guid value) => value.ToString("D");

    private static string When(DateTime value) => value.ToString("o", CultureInfo.InvariantCulture);

    private static string Dec(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Bool(bool value) => value ? "true" : "false";

    private static string PaymentMethodName(string raw)
    {
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            && Enum.IsDefined(typeof(PaymentMethod), value))
        {
            return ((PaymentMethod)value).ToString();
        }

        return raw;
    }
}
