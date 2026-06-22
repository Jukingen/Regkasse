using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Billing;

public interface IBillingService
{
    Task<LicenseSalePreviewResponse> PreviewLicenseSaleAsync(
        LicenseSalePreviewRequest request,
        CancellationToken ct = default);

    Task<LicenseSaleResponse> CreateLicenseSaleAsync(
        CreateLicenseSaleRequest request,
        Guid soldByUserId,
        CancellationToken ct = default);

    Task<LicenseSaleResponse> GetLicenseSaleAsync(
        Guid saleId,
        CancellationToken ct = default);

    Task<LicenseSaleListResponse> ListLicenseSalesAsync(
        LicenseSaleListQuery query,
        CancellationToken ct = default);

    Task<LicenseSaleResponse> CancelLicenseSaleAsync(
        Guid saleId,
        CancelLicenseSaleRequest request,
        Guid cancelledByUserId,
        CancellationToken ct = default);

    Task<LicenseSaleStatsResponse> GetLicenseSaleStatsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default);

    Task<byte[]> GenerateInvoicePdfAsync(
        Guid saleId,
        CancellationToken ct = default);

    Task<bool> CanExtendLicenseAsync(
        string licenseKey,
        CancellationToken ct = default);
}

public sealed class BillingService : IBillingService
{
    private const int MaxPageSize = 100;
    private const int ExpiringSoonDays = 30;
    private const string InvoiceStorageRelativePath = "data/license-invoices";

    private readonly AppDbContext _db;
    private readonly ILicenseKeyGenerator _licenseKeyGenerator;
    private readonly IInvoiceNumberGenerator _invoiceNumberGenerator;
    private readonly IWebHostEnvironment _environment;
    private readonly CompanyProfileOptions _sellerProfile;
    private readonly IBillingAuditService _billingAudit;
    private readonly IInvoicePdfGenerator _invoicePdfGenerator;
    private readonly ILogger<BillingService> _logger;

    public BillingService(
        AppDbContext db,
        ILicenseKeyGenerator licenseKeyGenerator,
        IInvoiceNumberGenerator invoiceNumberGenerator,
        IWebHostEnvironment environment,
        IOptions<CompanyProfileOptions> sellerProfile,
        IBillingAuditService billingAudit,
        IInvoicePdfGenerator invoicePdfGenerator,
        ILogger<BillingService> logger)
    {
        _db = db;
        _licenseKeyGenerator = licenseKeyGenerator;
        _invoiceNumberGenerator = invoiceNumberGenerator;
        _environment = environment;
        _sellerProfile = sellerProfile.Value;
        _billingAudit = billingAudit;
        _invoicePdfGenerator = invoicePdfGenerator;
        _logger = logger;
    }

    public async Task<LicenseSalePreviewResponse> PreviewLicenseSaleAsync(
        LicenseSalePreviewRequest request,
        CancellationToken ct = default)
    {
        var tenant = await LoadTenantAsync(request.TenantId, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Tenant not found.");

        var prepared = PrepareSaleComputation(tenant, request.LicensePlan, request.CustomValidUntilUtc, request.PriceNet, request.VatRate);
        var billingProfile = await LoadTenantBillingProfileAsync(tenant, ct).ConfigureAwait(false);
        var licenseKey = _licenseKeyGenerator.GenerateLicenseKey(tenant.Slug, prepared.ValidUntilUtc);
        var invoiceNumber = _invoiceNumberGenerator.GenerateInvoiceNumber(DateTime.UtcNow);

        return new LicenseSalePreviewResponse(
            LicenseKey: licenseKey,
            ValidFromUtc: prepared.ValidFromUtc,
            ValidUntilUtc: prepared.ValidUntilUtc,
            PriceNet: prepared.PriceNet,
            VatRate: prepared.VatRate,
            VatAmount: prepared.VatAmount,
            PriceGross: prepared.PriceGross,
            InvoiceNumber: invoiceNumber,
            TenantName: tenant.Name,
            TenantSlug: tenant.Slug,
            TenantAddress: billingProfile.Address,
            TenantVatId: billingProfile.VatId,
            TenantEmail: billingProfile.Email);
    }

    public async Task<LicenseSaleResponse> CreateLicenseSaleAsync(
        CreateLicenseSaleRequest request,
        Guid soldByUserId,
        CancellationToken ct = default)
    {
        EnsureActorUserId(soldByUserId);
        await EnsureUserExistsAsync(soldByUserId, ct).ConfigureAwait(false);

        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Tenant not found.");

        if (tenant.Status == TenantStatuses.Deleted)
            throw new InvalidOperationException("Deleted tenants cannot receive license sales.");

        var prepared = PrepareSaleComputation(
            tenant,
            request.LicensePlan,
            request.CustomValidUntilUtc,
            request.PriceNet,
            request.VatRate);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            var soldAtUtc = DateTime.UtcNow;
            var licenseKey = _licenseKeyGenerator.GenerateLicenseKey(tenant.Slug, prepared.ValidUntilUtc);
            if (!await CanExtendLicenseAsync(licenseKey, ct).ConfigureAwait(false))
                throw new InvalidOperationException("Generated license key cannot be assigned.");

            var invoiceNumber = _invoiceNumberGenerator.GenerateInvoiceNumber(soldAtUtc);
            var sale = new LicenseSale
            {
                TenantId = tenant.Id,
                LicenseKey = licenseKey,
                LicensePlan = prepared.Plan,
                CustomValidUntilUtc = prepared.Plan == LicenseSalePlans.Custom ? prepared.ValidUntilUtc : null,
                ValidFromUtc = prepared.ValidFromUtc,
                ValidUntilUtc = prepared.ValidUntilUtc,
                PriceNet = prepared.PriceNet,
                VatRate = prepared.VatRate,
                VatAmount = prepared.VatAmount,
                PriceGross = prepared.PriceGross,
                Currency = "EUR",
                SoldAtUtc = soldAtUtc,
                SoldByUserId = soldByUserId,
                InvoiceNumber = invoiceNumber,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                Status = LicenseSaleStatuses.Active,
                CreatedAt = soldAtUtc,
                UpdatedAt = soldAtUtc,
            };

            tenant.LicenseKey = licenseKey;
            tenant.LicenseValidUntilUtc = prepared.ValidUntilUtc;
            tenant.LicenseGracePeriodStartedAt = null;
            tenant.LicenseGracePeriodUsedDays = 0;
            tenant.UpdatedAt = soldAtUtc;

            _db.LicenseSales.Add(sale);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);

            await LogLicenseSaleAuditAsync(sale, soldByUserId, ct).ConfigureAwait(false);

            sale.Tenant = tenant;
            return MapResponse(sale);
        }
        catch
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<LicenseSaleResponse> GetLicenseSaleAsync(
        Guid saleId,
        CancellationToken ct = default)
    {
        var sale = await LoadSaleAsync(saleId, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("License sale not found.");

        return MapResponse(sale);
    }

    public async Task<LicenseSaleListResponse> ListLicenseSalesAsync(
        LicenseSaleListQuery query,
        CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        IQueryable<LicenseSale> salesQuery = LicenseSalesScope()
            .AsNoTracking()
            .Include(s => s.Tenant);

        if (Guid.TryParse(query.TenantId, out var tenantId))
            salesQuery = salesQuery.Where(s => s.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            if (!LicenseSaleStatuses.IsValid(status))
                throw new ArgumentException("Invalid status filter.", nameof(query));

            salesQuery = salesQuery.Where(s => s.Status == status);
        }

        if (query.FromDate.HasValue)
        {
            var fromUtc = ToUtcInstant(query.FromDate.Value);
            salesQuery = salesQuery.Where(s => s.SoldAtUtc >= fromUtc);
        }

        if (query.ToDate.HasValue)
        {
            var toUtc = ToUtcInstant(query.ToDate.Value);
            salesQuery = salesQuery.Where(s => s.SoldAtUtc <= toUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            salesQuery = salesQuery.Where(s =>
                s.InvoiceNumber.Contains(term)
                || s.LicenseKey.Contains(term)
                || (s.Tenant != null && (s.Tenant.Name.Contains(term) || s.Tenant.Slug.Contains(term))));
        }

        var totalCount = await salesQuery.CountAsync(ct).ConfigureAwait(false);
        var items = await salesQuery
            .OrderByDescending(s => s.SoldAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new LicenseSaleListResponse(
            Items: items.Select(MapResponse).ToList(),
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages);
    }

    public async Task<LicenseSaleResponse> CancelLicenseSaleAsync(
        Guid saleId,
        CancelLicenseSaleRequest request,
        Guid cancelledByUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CancellationReason))
            throw new ArgumentException("Cancellation reason is required.", nameof(request));

        EnsureActorUserId(cancelledByUserId);
        await EnsureUserExistsAsync(cancelledByUserId, ct).ConfigureAwait(false);

        var sale = await LicenseSalesScope()
            .Include(s => s.Tenant)
            .FirstOrDefaultAsync(s => s.Id == saleId, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("License sale not found.");

        if (sale.Status != LicenseSaleStatuses.Active)
            throw new InvalidOperationException($"License sale is already {sale.Status}.");

        var now = DateTime.UtcNow;
        sale.Status = LicenseSaleStatuses.Cancelled;
        sale.CancelledAtUtc = now;
        sale.CancelledByUserId = cancelledByUserId;
        sale.CancellationReason = request.CancellationReason.Trim();
        sale.UpdatedAt = now;

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        await _billingAudit.LogLicenseCancelledAsync(
            sale,
            cancelledByUserId,
            sale.CancellationReason ?? request.CancellationReason.Trim(),
            ipAddress: null,
            cancellationToken: ct).ConfigureAwait(false);
        _logger.LogInformation("License sale {SaleId} cancelled by {UserId}", saleId, cancelledByUserId);
        return MapResponse(sale);
    }

    public async Task<LicenseSaleStatsResponse> GetLicenseSaleStatsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expiringThreshold = now.AddDays(ExpiringSoonDays);

        IQueryable<LicenseSale> query = LicenseSalesScope()
            .AsNoTracking()
            .Where(s => s.Status == LicenseSaleStatuses.Active);

        if (fromDate.HasValue)
            query = query.Where(s => s.SoldAtUtc >= ToUtcInstant(fromDate.Value));

        if (toDate.HasValue)
            query = query.Where(s => s.SoldAtUtc <= ToUtcInstant(toDate.Value));

        var sales = await query.ToListAsync(ct).ConfigureAwait(false);

        return new LicenseSaleStatsResponse(
            TotalRevenueNet: sales.Sum(s => s.PriceNet),
            TotalRevenueGross: sales.Sum(s => s.PriceGross),
            TotalVat: sales.Sum(s => s.VatAmount),
            TotalSales: sales.Count,
            ActiveLicenses: sales.Count(s => s.ValidUntilUtc > now),
            ExpiringSoonLicenses: sales.Count(s => s.ValidUntilUtc > now && s.ValidUntilUtc <= expiringThreshold));
    }

    public async Task<byte[]> GenerateInvoicePdfAsync(
        Guid saleId,
        CancellationToken ct = default)
    {
        var sale = await LicenseSalesScope()
            .Include(s => s.Tenant)
            .FirstOrDefaultAsync(s => s.Id == saleId, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("License sale not found.");

        var tenant = sale.Tenant
            ?? throw new InvalidOperationException("License sale tenant not found.");

        var billingProfile = await LoadTenantBillingProfileAsync(tenant, ct).ConfigureAwait(false);
        var logoBytes = InvoicePdfGenerator.TryLoadLogoBytes(_sellerProfile.LogoUrl, _environment.ContentRootPath);
        var pdf = _invoicePdfGenerator.Generate(new LicenseSaleInvoiceDocument(
            Sale: sale,
            TenantName: tenant.Name,
            TenantSlug: tenant.Slug,
            TenantAddress: billingProfile.Address,
            TenantVatId: string.IsNullOrWhiteSpace(billingProfile.VatId) ? null : billingProfile.VatId,
            TenantEmail: string.IsNullOrWhiteSpace(billingProfile.Email) ? null : billingProfile.Email,
            Seller: _sellerProfile,
            SellerLogoBytes: logoBytes));
        var relativePath = Path.Combine(InvoiceStorageRelativePath, $"{sale.InvoiceNumber}.pdf");
        var absolutePath = Path.Combine(_environment.ContentRootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await File.WriteAllBytesAsync(absolutePath, pdf, ct).ConfigureAwait(false);

        if (!string.Equals(sale.InvoicePdfPath, relativePath, StringComparison.Ordinal))
        {
            sale.InvoicePdfPath = relativePath.Replace('\\', '/');
            sale.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return pdf;
    }

    public async Task<bool> CanExtendLicenseAsync(
        string licenseKey,
        CancellationToken ct = default)
    {
        if (!_licenseKeyGenerator.ValidateLicenseKeyFormat(licenseKey))
            return false;

        var key = licenseKey.Trim();
        var hasActiveSale = await LicenseSalesScope().AsNoTracking()
            .AnyAsync(s => s.LicenseKey == key && s.Status == LicenseSaleStatuses.Active, ct)
            .ConfigureAwait(false);
        if (hasActiveSale)
            return false;

        var now = DateTime.UtcNow;
        var assignedElsewhere = await _db.Tenants.AsNoTracking()
            .AnyAsync(
                t => t.LicenseKey == key
                     && t.Status != TenantStatuses.Deleted
                     && (t.LicenseValidUntilUtc == null || t.LicenseValidUntilUtc > now),
                ct)
            .ConfigureAwait(false);

        return !assignedElsewhere;
    }

    private async Task<Tenant?> LoadTenantAsync(Guid tenantId, CancellationToken ct)
    {
        return await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            .ConfigureAwait(false);
    }

    private async Task<LicenseSale?> LoadSaleAsync(Guid saleId, CancellationToken ct) =>
        await LicenseSalesScope()
            .Include(s => s.Tenant)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == saleId, ct)
            .ConfigureAwait(false);

    /// <summary>Super Admin billing crosses tenants; bypass tenant query filter.</summary>
    private IQueryable<LicenseSale> LicenseSalesScope() => _db.LicenseSales.IgnoreQueryFilters();

    private async Task<(string Address, string VatId, string Email)> LoadTenantBillingProfileAsync(
        Tenant tenant,
        CancellationToken ct)
    {
        var companySettings = await _db.CompanySettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenant.Id, ct)
            .ConfigureAwait(false);

        return (
            Address: tenant.Address ?? companySettings?.CompanyAddress ?? string.Empty,
            VatId: companySettings?.CompanyTaxNumber ?? string.Empty,
            Email: tenant.Email ?? companySettings?.CompanyEmail ?? string.Empty);
    }

    private static PreparedLicenseSale PrepareSaleComputation(
        Tenant tenant,
        string licensePlan,
        DateTime? customValidUntilUtc,
        decimal priceNet,
        decimal vatRate)
    {
        if (!LicenseSalePlans.IsValid(licensePlan))
            throw new ArgumentException("Invalid license plan.", nameof(licensePlan));

        if (priceNet <= 0)
            throw new ArgumentException("PriceNet must be greater than zero.", nameof(priceNet));

        if (vatRate < 0)
            throw new ArgumentException("VatRate cannot be negative.", nameof(vatRate));

        var plan = licensePlan.Trim();
        var now = DateTime.UtcNow;
        var validFromUtc = tenant.LicenseValidUntilUtc.HasValue && tenant.LicenseValidUntilUtc.Value > now
            ? DateTime.SpecifyKind(tenant.LicenseValidUntilUtc.Value, DateTimeKind.Utc)
            : now;

        DateTime validUntilUtc = plan switch
        {
            LicenseSalePlans.SixMonths => validFromUtc.AddMonths(6),
            LicenseSalePlans.TwelveMonths => validFromUtc.AddMonths(12),
            LicenseSalePlans.Custom => ResolveCustomValidUntil(customValidUntilUtc, validFromUtc),
            _ => throw new ArgumentException("Invalid license plan.", nameof(licensePlan)),
        };

        var vatAmount = Math.Round(priceNet * vatRate / 100m, 2, MidpointRounding.AwayFromZero);
        var priceGross = priceNet + vatAmount;

        return new PreparedLicenseSale(
            Plan: plan,
            ValidFromUtc: validFromUtc,
            ValidUntilUtc: validUntilUtc,
            PriceNet: priceNet,
            VatRate: vatRate,
            VatAmount: vatAmount,
            PriceGross: priceGross);
    }

    private static DateTime ResolveCustomValidUntil(DateTime? customValidUntilUtc, DateTime validFromUtc)
    {
        if (!customValidUntilUtc.HasValue)
            throw new ArgumentException("CustomValidUntilUtc is required for custom license plans.");

        var validUntilUtc = DateTime.SpecifyKind(customValidUntilUtc.Value, DateTimeKind.Utc);
        if (validUntilUtc <= validFromUtc)
            throw new ArgumentException("CustomValidUntilUtc must be after the license start date.");

        return validUntilUtc;
    }

    private static LicenseSaleResponse MapResponse(LicenseSale sale)
    {
        var tenant = sale.Tenant
            ?? throw new InvalidOperationException("License sale tenant is not loaded.");

        return new LicenseSaleResponse(
            Id: sale.Id,
            TenantId: sale.TenantId,
            TenantName: tenant.Name,
            TenantSlug: tenant.Slug,
            LicenseKey: sale.LicenseKey,
            LicensePlan: sale.LicensePlan,
            ValidFromUtc: sale.ValidFromUtc,
            ValidUntilUtc: sale.ValidUntilUtc,
            PriceNet: sale.PriceNet,
            VatRate: sale.VatRate,
            VatAmount: sale.VatAmount,
            PriceGross: sale.PriceGross,
            Currency: sale.Currency,
            InvoiceNumber: sale.InvoiceNumber,
            InvoicePdfPath: sale.InvoicePdfPath,
            Status: sale.Status,
            SoldAtUtc: sale.SoldAtUtc,
            SoldByUserId: sale.SoldByUserId.ToString("D"),
            Notes: sale.Notes);
    }

    private async Task EnsureUserExistsAsync(Guid actorUserId, CancellationToken ct)
    {
        var actorUserIdText = actorUserId.ToString("D");
        var exists = await _db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == actorUserIdText, ct)
            .ConfigureAwait(false);
        if (!exists)
            throw new ArgumentException("User not found.");
    }

    private Task LogLicenseSaleAuditAsync(
        LicenseSale sale,
        Guid actorUserId,
        CancellationToken ct) =>
        _billingAudit.LogLicenseSoldAsync(sale, actorUserId, ipAddress: null, cancellationToken: ct);

    private static void EnsureActorUserId(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User id is required.", nameof(userId));
    }

    private static DateTime ToUtcInstant(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    private sealed record PreparedLicenseSale(
        string Plan,
        DateTime ValidFromUtc,
        DateTime ValidUntilUtc,
        decimal PriceNet,
        decimal VatRate,
        decimal VatAmount,
        decimal PriceGross);
}
