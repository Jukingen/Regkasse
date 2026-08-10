using System.Globalization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SaasLicenseType = KasseAPI_Final.Models.Enums.LicenseType;

namespace KasseAPI_Final.Services.Billing;

public sealed class SubscriptionInvoiceService : ISubscriptionInvoiceService
{
    private const string StorageRelativePath = "data/subscription-invoices";
    private const decimal DefaultVatRate = 20m;

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly BillingOptions _options;
    private readonly ILogger<SubscriptionInvoiceService> _logger;

    public SubscriptionInvoiceService(
        AppDbContext db,
        IWebHostEnvironment environment,
        IOptions<BillingOptions> options,
        ILogger<SubscriptionInvoiceService> logger)
    {
        _db = db;
        _environment = environment;
        _options = options.Value;
        _logger = logger;
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    public async Task<IReadOnlyList<SubscriptionInvoiceDto>> ListAsync(
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var rows = await (
                from inv in _db.SubscriptionInvoices.AsNoTracking().IgnoreQueryFilters()
                join t in _db.Tenants.AsNoTracking().IgnoreQueryFilters() on inv.TenantId equals t.Id
                orderby inv.IssuedAtUtc descending
                select new SubscriptionInvoiceDto
                {
                    Id = inv.Id,
                    TenantId = inv.TenantId,
                    TenantName = t.Name,
                    TenantSlug = t.Slug,
                    InvoiceNumber = inv.InvoiceNumber,
                    PeriodStartUtc = inv.PeriodStartUtc,
                    PeriodEndUtc = inv.PeriodEndUtc,
                    LicenseType = inv.LicenseType,
                    AmountNet = inv.AmountNet,
                    VatRate = inv.VatRate,
                    AmountVat = inv.AmountVat,
                    AmountGross = inv.AmountGross,
                    Currency = inv.Currency,
                    Status = inv.Status,
                    IssuedAtUtc = inv.IssuedAtUtc,
                })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows;
    }

    public async Task<SubscriptionInvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await (
                from inv in _db.SubscriptionInvoices.AsNoTracking().IgnoreQueryFilters()
                join t in _db.Tenants.AsNoTracking().IgnoreQueryFilters() on inv.TenantId equals t.Id
                where inv.Id == id
                select new SubscriptionInvoiceDto
                {
                    Id = inv.Id,
                    TenantId = inv.TenantId,
                    TenantName = t.Name,
                    TenantSlug = t.Slug,
                    InvoiceNumber = inv.InvoiceNumber,
                    PeriodStartUtc = inv.PeriodStartUtc,
                    PeriodEndUtc = inv.PeriodEndUtc,
                    LicenseType = inv.LicenseType,
                    AmountNet = inv.AmountNet,
                    VatRate = inv.VatRate,
                    AmountVat = inv.AmountVat,
                    AmountGross = inv.AmountGross,
                    Currency = inv.Currency,
                    Status = inv.Status,
                    IssuedAtUtc = inv.IssuedAtUtc,
                })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<byte[]?> GetPdfAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var inv = await _db.SubscriptionInvoices.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (inv == null)
            return null;

        if (!string.IsNullOrWhiteSpace(inv.PdfPath) && File.Exists(inv.PdfPath))
            return await File.ReadAllBytesAsync(inv.PdfPath, cancellationToken).ConfigureAwait(false);

        var dto = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return dto == null ? null : BuildPdf(dto);
    }

    public async Task<MonthlyInvoiceGenerationResult> GenerateMonthlyInvoicesAsync(
        DateTime? periodMonthUtc = null,
        CancellationToken cancellationToken = default)
    {
        var result = new MonthlyInvoiceGenerationResult();
        var anchor = periodMonthUtc ?? DateTime.UtcNow.AddMonths(-1);
        var periodStart = new DateTime(anchor.Year, anchor.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);

        var activeTenants = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .Where(t => t.Status == TenantStatuses.Active && t.IsActive)
            .Select(t => new { t.Id, t.Name, t.Slug })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var tenant in activeTenants)
        {
            try
            {
                var exists = await _db.SubscriptionInvoices.IgnoreQueryFilters()
                    .AnyAsync(
                        i => i.TenantId == tenant.Id
                             && i.PeriodStartUtc == periodStart
                             && i.PeriodEndUtc == periodEnd,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (exists)
                {
                    result.Skipped++;
                    continue;
                }

                var licenseType = await ResolveLicenseTypeAsync(tenant.Id, cancellationToken).ConfigureAwait(false);
                var amountNet = ResolveMonthlyNet(licenseType);
                if (amountNet <= 0)
                {
                    result.Skipped++;
                    continue;
                }

                var vatRate = DefaultVatRate;
                var amountVat = Math.Round(amountNet * vatRate / 100m, 2, MidpointRounding.AwayFromZero);
                var amountGross = amountNet + amountVat;
                var invoiceNumber =
                    $"SUB-{periodStart:yyyyMM}-{tenant.Slug.ToUpperInvariant()}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

                var entity = new SubscriptionInvoice
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    InvoiceNumber = invoiceNumber,
                    PeriodStartUtc = periodStart,
                    PeriodEndUtc = periodEnd,
                    LicenseType = licenseType,
                    AmountNet = amountNet,
                    VatRate = vatRate,
                    AmountVat = amountVat,
                    AmountGross = amountGross,
                    Currency = "EUR",
                    Status = SubscriptionInvoiceStatuses.Issued,
                    IssuedAtUtc = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                };

                var dto = new SubscriptionInvoiceDto
                {
                    Id = entity.Id,
                    TenantId = tenant.Id,
                    TenantName = tenant.Name,
                    TenantSlug = tenant.Slug,
                    InvoiceNumber = invoiceNumber,
                    PeriodStartUtc = periodStart,
                    PeriodEndUtc = periodEnd,
                    LicenseType = licenseType,
                    AmountNet = amountNet,
                    VatRate = vatRate,
                    AmountVat = amountVat,
                    AmountGross = amountGross,
                    Currency = "EUR",
                    Status = entity.Status,
                    IssuedAtUtc = entity.IssuedAtUtc,
                };

                entity.PdfPath = await PersistPdfAsync(dto, cancellationToken).ConfigureAwait(false);
                _db.SubscriptionInvoices.Add(entity);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                result.Created++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                _logger.LogError(ex, "Failed to generate subscription invoice for tenant {TenantId}", tenant.Id);
            }
        }

        return result;
    }

    private async Task<SaasLicenseType> ResolveLicenseTypeAsync(Guid tenantId, CancellationToken ct)
    {
        var type = await _db.LicenseSales.AsNoTracking().IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && s.Status == LicenseSaleStatuses.Active)
            .OrderByDescending(s => s.ValidUntilUtc)
            .Select(s => (SaasLicenseType?)s.LicenseType)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return type ?? SaasLicenseType.Starter;
    }

    private decimal ResolveMonthlyNet(SaasLicenseType type) => type switch
    {
        SaasLicenseType.Trial => 0m,
        SaasLicenseType.Starter => _options.MonthlyNetStarter,
        SaasLicenseType.Business => _options.MonthlyNetBusiness,
        SaasLicenseType.Plus => _options.MonthlyNetPlus,
        _ => _options.MonthlyNetStarter,
    };

    private async Task<string> PersistPdfAsync(SubscriptionInvoiceDto dto, CancellationToken ct)
    {
        var root = Path.Combine(_environment.ContentRootPath, StorageRelativePath);
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"{dto.InvoiceNumber}.pdf");
        var bytes = BuildPdf(dto);
        await File.WriteAllBytesAsync(path, bytes, ct).ConfigureAwait(false);
        return path;
    }

    private static byte[] BuildPdf(SubscriptionInvoiceDto dto)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Header().Text($"Rechnung {dto.InvoiceNumber}").SemiBold().FontSize(18);
                page.Content().Column(col =>
                {
                    col.Item().Text($"Mandant: {dto.TenantName} ({dto.TenantSlug})");
                    col.Item().Text(
                        $"Zeitraum: {dto.PeriodStartUtc:yyyy-MM-dd} – {dto.PeriodEndUtc.AddDays(-1):yyyy-MM-dd} UTC");
                    col.Item().Text($"Paket: {dto.LicenseType}");
                    col.Item().PaddingTop(12).Text(
                        $"Netto: {dto.AmountNet.ToString("0.00", CultureInfo.InvariantCulture)} {dto.Currency}");
                    col.Item().Text(
                        $"USt ({dto.VatRate.ToString("0.##", CultureInfo.InvariantCulture)}%): {dto.AmountVat.ToString("0.00", CultureInfo.InvariantCulture)} {dto.Currency}");
                    col.Item().Text(
                        $"Brutto: {dto.AmountGross.ToString("0.00", CultureInfo.InvariantCulture)} {dto.Currency}").SemiBold();
                });
                page.Footer().AlignCenter().Text("Regkasse SaaS — kein fiskalischer Beleg").FontSize(9).FontColor(Colors.Grey.Medium);
            });
        });

        return doc.GeneratePdf();
    }
}
