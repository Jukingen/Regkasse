using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Billing;

/// <summary>
/// Maps <c>license_sales</c> to tenant-facing invoices.
/// Isolation: explicit <c>tenantId</c> filter (never client-supplied from another tenant).
/// Cross-tenant / missing sale → <see cref="KeyNotFoundException"/> (HTTP 404).
/// </summary>
public sealed class TenantInvoiceService : ITenantInvoiceService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly AppDbContext _db;
    private readonly IInvoicePdfGenerator _pdfGenerator;

    public TenantInvoiceService(AppDbContext db, IInvoicePdfGenerator pdfGenerator)
    {
        _db = db;
        _pdfGenerator = pdfGenerator;
    }

    public async Task<TenantInvoiceListResponse> GetInvoicesForTenantAsync(
        Guid tenantId,
        int page = 1,
        int pageSize = 20,
        string? status = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = pageSize <= 0 ? DefaultPageSize : Math.Clamp(pageSize, 1, MaxPageSize);

        if (tenantId == Guid.Empty)
        {
            return EmptyPage(page, pageSize);
        }

        if (!TenantInvoiceStatuses.TryMapFilter(status, out var licenseSaleStatus, out var matchNone))
        {
            return EmptyPage(page, pageSize);
        }

        if (matchNone)
        {
            return EmptyPage(page, pageSize);
        }

        var query = SalesForTenant(_db, tenantId);

        if (licenseSaleStatus is not null)
            query = query.Where(s => s.Status == licenseSaleStatus);

        if (fromUtc.HasValue)
            query = query.Where(s => s.SoldAtUtc >= ToUtcInstant(fromUtc.Value));

        if (toUtc.HasValue)
            query = query.Where(s => s.SoldAtUtc <= ToUtcInstant(toUtc.Value));

        var statusCounts = await query
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalCount = statusCounts.Sum(x => x.Count);
        var activeCount = statusCounts
            .Where(x => string.Equals(x.Status, LicenseSaleStatuses.Active, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Count);
        var cancelledCount = statusCounts
            .Where(x =>
                string.Equals(x.Status, LicenseSaleStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Status, LicenseSaleStatuses.Refunded, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Count);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var rows = await query
            .OrderByDescending(s => s.SoldAtUtc)
            .ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new TenantInvoiceListResponse
        {
            Items = rows.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            ActiveCount = activeCount,
            CancelledCount = cancelledCount,
        };
    }

    public async Task<(byte[] Pdf, string FileName)> GetInvoicePdfForTenantAsync(
        Guid tenantId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || invoiceId == Guid.Empty)
            throw new KeyNotFoundException("Invoice not found.");

        var sale = await SalesForTenant(_db, tenantId)
            .Include(s => s.Tenant)
            .FirstOrDefaultAsync(s => s.Id == invoiceId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Invoice not found.");

        var pdf = await _pdfGenerator.GenerateInvoicePdfAsync(invoiceId, cancellationToken)
            .ConfigureAwait(false);

        var slug = sale.Tenant?.Slug?.Trim();
        var fileName = string.IsNullOrWhiteSpace(slug)
            ? $"RE-{sale.InvoiceNumber}.pdf"
            : $"RE-{sale.InvoiceNumber}-{slug}.pdf";

        return (pdf, fileName);
    }

    private static TenantInvoiceListResponse EmptyPage(int page, int pageSize) => new()
    {
        Items = [],
        TotalCount = 0,
        Page = page,
        PageSize = pageSize,
        TotalPages = 0,
        ActiveCount = 0,
        CancelledCount = 0,
    };

    private static IQueryable<LicenseSale> SalesForTenant(AppDbContext db, Guid tenantId) =>
        db.LicenseSales
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId);

    private static TenantInvoiceDto MapToDto(LicenseSale sale)
    {
        var pdfUrl = $"/api/admin/billing/tenant-invoices/{sale.Id:D}/pdf";
        var issuedAt = sale.SoldAtUtc;
        var licenseKey = string.IsNullOrWhiteSpace(sale.LicenseKey) ? null : sale.LicenseKey;
        var licensePlan = string.IsNullOrWhiteSpace(sale.LicensePlan) ? null : sale.LicensePlan;

        return new TenantInvoiceDto
        {
            Id = sale.Id,
            InvoiceNumber = sale.InvoiceNumber,
            IssuedAt = issuedAt,
            InvoiceDateUtc = issuedAt,
            AmountNet = sale.PriceNet,
            VatAmount = sale.VatAmount,
            AmountGross = sale.PriceGross,
            Currency = string.IsNullOrWhiteSpace(sale.Currency) ? "EUR" : sale.Currency,
            Status = TenantInvoiceStatuses.FromLicenseSaleStatus(sale.Status),
            LicenseKey = licenseKey,
            LicensePlan = licensePlan,
            DownloadUrl = pdfUrl,
            PdfUrl = pdfUrl,
        };
    }

    private static DateTime ToUtcInstant(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
