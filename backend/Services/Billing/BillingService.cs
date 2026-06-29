using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Billing;

public sealed class BillingService : IBillingService
{
    private const int MaxPageSize = 100;
    private const int ExpiringSoonDays = 30;
    private const string InvoiceStorageRelativePath = "data/license-invoices";

    private static readonly List<LicensePlanDefinition> Plans =
    [
        new LicensePlanDefinition { Plan = LicenseSalePlans.SixMonths, DurationDays = 180, DisplayName = "6 Monate" },
        new LicensePlanDefinition { Plan = LicenseSalePlans.TwelveMonths, DurationDays = 365, DisplayName = "1 Jahr" },
        new LicensePlanDefinition { Plan = LicenseSalePlans.Custom, DurationDays = 0, DisplayName = "Benutzerdefiniert" },
    ];

    private readonly AppDbContext _dbContext;
    private readonly ILicenseKeyGenerator _licenseKeyGenerator;
    private readonly IBillingAuditService _billingAudit;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWebHostEnvironment _environment;
    private readonly IInvoicePdfGenerator _invoicePdfGenerator;
    private readonly BillingBackupConfig _backupConfig;
    private readonly ILogger<BillingService> _logger;

    public BillingService(
        AppDbContext dbContext,
        ILicenseKeyGenerator licenseKeyGenerator,
        IBillingAuditService billingAudit,
        IServiceScopeFactory scopeFactory,
        IWebHostEnvironment environment,
        IInvoicePdfGenerator invoicePdfGenerator,
        IOptions<BillingBackupConfig> backupConfig,
        ILogger<BillingService> logger)
    {
        _dbContext = dbContext;
        _licenseKeyGenerator = licenseKeyGenerator;
        _billingAudit = billingAudit;
        _scopeFactory = scopeFactory;
        _environment = environment;
        _invoicePdfGenerator = invoicePdfGenerator;
        _backupConfig = backupConfig.Value;
        _logger = logger;
    }

    public async Task<LicenseSalePreviewResponse> PreviewLicenseSaleAsync(
        LicenseSalePreviewRequest request,
        CancellationToken ct = default)
    {
        var db = _dbContext;

        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Tenant {request.TenantId} not found");

        ValidatePricing(request.PriceNet, request.VatRate);
        var (validFrom, validUntil) = GetValidityPeriod(tenant, request.LicensePlan, request.CustomValidUntilUtc);
        var licenseKey = _licenseKeyGenerator.GenerateLicenseKey(tenant.Slug, validUntil);
        var invoiceNumber = await AllocateInvoiceNumberAsync(db, DateTime.UtcNow, ct).ConfigureAwait(false);
        var (vatAmount, priceGross) = CalculateAmounts(request.PriceNet, request.VatRate);
        var durationDays = ResolveDurationDays(request.LicensePlan, validFrom, validUntil);
        var billingProfile = await LoadTenantBillingProfileAsync(db, tenant, ct).ConfigureAwait(false);

        return new LicenseSalePreviewResponse
        {
            LicenseKey = licenseKey,
            LicensePlan = request.LicensePlan,
            ValidFromUtc = validFrom,
            ValidUntilUtc = validUntil,
            DurationDays = durationDays,
            DurationDisplay = GetDurationDisplay(durationDays, request.LicensePlan),
            PriceNet = request.PriceNet,
            VatRate = request.VatRate,
            VatAmount = vatAmount,
            PriceGross = priceGross,
            InvoiceNumber = invoiceNumber,
            TenantName = tenant.Name,
            TenantSlug = tenant.Slug,
            TenantAddress = NullIfEmpty(billingProfile.Address),
            TenantVatId = NullIfEmpty(billingProfile.VatId),
            TenantEmail = NullIfEmpty(billingProfile.Email),
            Currency = "EUR",
        };
    }

    public async Task<LicenseSaleResponse> CreateLicenseSaleAsync(
        CreateLicenseSaleRequest request,
        Guid soldByUserId,
        CancellationToken ct = default)
    {
        EnsureActorUserId(soldByUserId);
        var db = _dbContext;
        await EnsureUserExistsAsync(db, soldByUserId, ct).ConfigureAwait(false);

        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            var tenant = await db.Tenants
                .FirstOrDefaultAsync(t => t.Id == request.TenantId, ct)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"Tenant {request.TenantId} not found");

            if (tenant.Status == TenantStatuses.Deleted)
                throw new InvalidOperationException("Deleted tenants cannot receive license sales.");

            ValidatePricing(request.PriceNet, request.VatRate);
            var (validFrom, validUntil) = GetValidityPeriod(tenant, request.LicensePlan, request.CustomValidUntilUtc);
            var licenseKey = _licenseKeyGenerator.GenerateLicenseKey(tenant.Slug, validUntil);

            var keyExists = await LicenseSalesScope(db)
                .AnyAsync(l => l.LicenseKey == licenseKey, ct)
                .ConfigureAwait(false);
            if (keyExists)
                throw new InvalidOperationException("License key collision detected");

            if (!await IsLicenseKeyAvailableAsync(db, licenseKey, ct).ConfigureAwait(false))
                throw new InvalidOperationException("Generated license key cannot be assigned.");

            var soldAtUtc = DateTime.UtcNow;
            var invoiceNumber = await AllocateInvoiceNumberAsync(db, soldAtUtc, ct).ConfigureAwait(false);
            var (vatAmount, priceGross) = CalculateAmounts(request.PriceNet, request.VatRate);
            var plan = request.LicensePlan.Trim();

            var sale = new LicenseSale
            {
                TenantId = request.TenantId,
                LicenseKey = licenseKey,
                LicensePlan = plan,
                CustomValidUntilUtc = plan == LicenseSalePlans.Custom ? validUntil : null,
                ValidFromUtc = validFrom,
                ValidUntilUtc = validUntil,
                PriceNet = request.PriceNet,
                VatRate = request.VatRate,
                VatAmount = vatAmount,
                PriceGross = priceGross,
                Currency = "EUR",
                SoldAtUtc = soldAtUtc,
                SoldByUserId = soldByUserId,
                InvoiceNumber = invoiceNumber,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                Status = LicenseSaleStatuses.Active,
                CreatedAt = soldAtUtc,
                UpdatedAt = soldAtUtc,
            };

            db.LicenseSales.Add(sale);

            tenant.CurrentLicenseSaleId = sale.Id;
            tenant.LicenseKey = licenseKey;
            tenant.LicenseValidUntilUtc = validUntil;
            tenant.LastLicenseActivationUtc = soldAtUtc;
            tenant.LicenseActivationCount++;
            tenant.LicenseGracePeriodStartedAt = null;
            tenant.LicenseGracePeriodUsedDays = 0;
            tenant.UpdatedAt = soldAtUtc;

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);

            await _billingAudit.LogLicenseSoldAsync(sale, soldByUserId, ipAddress: null, cancellationToken: ct)
                .ConfigureAwait(false);

            await ScheduleRemindersForSaleAsync(sale.Id, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "License sale created: {InvoiceNumber} for tenant {TenantSlug}",
                invoiceNumber,
                tenant.Slug);

            sale.Tenant = tenant;
            var response = await MapToResponseAsync(sale, db, ct).ConfigureAwait(false);
            TriggerSaleBackupIfEnabled(sale.Id);
            return response;
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
        var db = _dbContext;

        var sale = await LicenseSalesScope(db)
            .Include(l => l.Tenant)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == saleId, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Sale {saleId} not found");

        return await MapToResponseAsync(sale, db, ct).ConfigureAwait(false);
    }

    public async Task<LicenseSaleListResponse> ListLicenseSalesAsync(
        LicenseSaleListQuery query,
        CancellationToken ct = default)
    {
        var db = _dbContext;

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var salesQuery = LicenseSalesScope(db)
            .Include(l => l.Tenant)
            .AsNoTracking();

        if (query.TenantId.HasValue)
            salesQuery = salesQuery.Where(l => l.TenantId == query.TenantId.Value);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
            {
                if (!LicenseSaleStatuses.IsValid(status))
                    throw new ArgumentException("Invalid status filter.", nameof(query));

                salesQuery = salesQuery.Where(l => l.Status == status);
            }
        }

        if (query.FromDate.HasValue)
            salesQuery = salesQuery.Where(l => l.SoldAtUtc >= ToUtcInstant(query.FromDate.Value));

        if (query.ToDate.HasValue)
            salesQuery = salesQuery.Where(l => l.SoldAtUtc <= ToUtcInstant(query.ToDate.Value));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            salesQuery = salesQuery.Where(l =>
                l.InvoiceNumber.Contains(search)
                || l.LicenseKey.Contains(search)
                || (l.Tenant != null && (l.Tenant.Name.Contains(search) || l.Tenant.Slug.Contains(search))));
        }

        var totalCount = await salesQuery.CountAsync(ct).ConfigureAwait(false);
        var items = await salesQuery
            .OrderByDescending(l => l.SoldAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var userNames = await LoadUserDisplayNamesAsync(db, items, ct).ConfigureAwait(false);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new LicenseSaleListResponse
        {
            Items = items.Select(item => MapToResponse(item, userNames)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
        };
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
        var db = _dbContext;
        await EnsureUserExistsAsync(db, cancelledByUserId, ct).ConfigureAwait(false);

        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            var sale = await LicenseSalesScope(db)
                .Include(l => l.Tenant)
                .FirstOrDefaultAsync(l => l.Id == saleId, ct)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"Sale {saleId} not found");

            if (sale.Status != LicenseSaleStatuses.Active)
                throw new InvalidOperationException($"Sale is already {sale.Status}");

            var now = DateTime.UtcNow;
            var reason = request.CancellationReason.Trim();
            sale.Status = LicenseSaleStatuses.Cancelled;
            sale.CancelledAtUtc = now;
            sale.CancelledByUserId = cancelledByUserId;
            sale.CancellationReason = reason;
            sale.UpdatedAt = now;

            var tenant = sale.Tenant;
            if (tenant.CurrentLicenseSaleId == sale.Id)
            {
                tenant.CurrentLicenseSaleId = null;
                tenant.LicenseKey = null;
                tenant.LicenseValidUntilUtc = null;
                tenant.UpdatedAt = now;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);

            await _billingAudit.LogLicenseCancelledAsync(sale, cancelledByUserId, reason, ipAddress: null, cancellationToken: ct)
                .ConfigureAwait(false);

            await CancelRemindersForSaleAsync(sale.Id, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "License sale cancelled: {InvoiceNumber} for tenant {TenantSlug}",
                sale.InvoiceNumber,
                tenant.Slug);

            return await MapToResponseAsync(sale, db, ct).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<LicenseSaleStatsResponse> GetLicenseSaleStatsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var db = _dbContext;
        var now = DateTime.UtcNow;
        var expiringThreshold = now.AddDays(ExpiringSoonDays);

        var revenueQuery = LicenseSalesScope(db).AsNoTracking();
        if (fromDate.HasValue)
            revenueQuery = revenueQuery.Where(l => l.SoldAtUtc >= ToUtcInstant(fromDate.Value));
        if (toDate.HasValue)
            revenueQuery = revenueQuery.Where(l => l.SoldAtUtc <= ToUtcInstant(toDate.Value));

        var activeRevenueQuery = revenueQuery.Where(l => l.Status == LicenseSaleStatuses.Active);
        var totalRevenueNet = await activeRevenueQuery.SumAsync(l => l.PriceNet, ct).ConfigureAwait(false);
        var totalRevenueGross = await activeRevenueQuery.SumAsync(l => l.PriceGross, ct).ConfigureAwait(false);
        var totalVat = await activeRevenueQuery.SumAsync(l => l.VatAmount, ct).ConfigureAwait(false);
        var totalSales = await activeRevenueQuery.CountAsync(ct).ConfigureAwait(false);

        var activeLicenses = await LicenseSalesScope(db)
            .AsNoTracking()
            .CountAsync(l => l.Status == LicenseSaleStatuses.Active && l.ValidUntilUtc > now, ct)
            .ConfigureAwait(false);

        var expiringSoon = await LicenseSalesScope(db)
            .AsNoTracking()
            .CountAsync(
                l => l.Status == LicenseSaleStatuses.Active
                     && l.ValidUntilUtc > now
                     && l.ValidUntilUtc <= expiringThreshold,
                ct)
            .ConfigureAwait(false);

        var expired = await LicenseSalesScope(db)
            .AsNoTracking()
            .CountAsync(l => l.Status == LicenseSaleStatuses.Active && l.ValidUntilUtc <= now, ct)
            .ConfigureAwait(false);

        var cancelledQuery = LicenseSalesScope(db).AsNoTracking()
            .Where(l => l.Status == LicenseSaleStatuses.Cancelled || l.Status == LicenseSaleStatuses.Refunded);
        if (fromDate.HasValue)
            cancelledQuery = cancelledQuery.Where(l => l.SoldAtUtc >= ToUtcInstant(fromDate.Value));
        if (toDate.HasValue)
            cancelledQuery = cancelledQuery.Where(l => l.SoldAtUtc <= ToUtcInstant(toDate.Value));

        var cancelled = await cancelledQuery.CountAsync(ct).ConfigureAwait(false);

        var tenantsWithLicense = await db.Tenants
            .AsNoTracking()
            .CountAsync(t => t.CurrentLicenseSaleId != null, ct)
            .ConfigureAwait(false);

        return new LicenseSaleStatsResponse
        {
            TotalRevenueNet = totalRevenueNet,
            TotalRevenueGross = totalRevenueGross,
            TotalVat = totalVat,
            TotalSales = totalSales,
            ActiveLicenses = activeLicenses,
            ExpiringSoonLicenses = expiringSoon,
            ExpiredLicenses = expired,
            CancelledSales = cancelled,
            AveragePriceNet = totalSales > 0
                ? Math.Round(totalRevenueNet / totalSales, 2, MidpointRounding.AwayFromZero)
                : 0m,
            TotalTenantsWithLicense = tenantsWithLicense,
        };
    }

    public async Task<LicenseSaleResponse?> GetSaleByLicenseKeyAsync(
        string licenseKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return null;

        var db = _dbContext;

        var sale = await LicenseSalesScope(db)
            .Include(l => l.Tenant)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LicenseKey == licenseKey.Trim(), ct)
            .ConfigureAwait(false);

        return sale == null ? null : await MapToResponseAsync(sale, db, ct).ConfigureAwait(false);
    }

    public async Task<bool> IsLicenseKeyValidAsync(
        string licenseKey,
        CancellationToken ct = default)
    {
        var db = _dbContext;
        return await IsLicenseKeyAvailableAsync(db, licenseKey, ct).ConfigureAwait(false);
    }

    public async Task<string> GetNextInvoiceNumberAsync(DateTime date)
    {
        var db = _dbContext;
        return await AllocateInvoiceNumberAsync(db, date).ConfigureAwait(false);
    }

    public async Task<byte[]> GenerateInvoicePdfAsync(
        Guid saleId,
        CancellationToken ct = default)
    {
        var pdf = await _invoicePdfGenerator.GenerateInvoicePdfAsync(saleId, ct).ConfigureAwait(false);

        var db = _dbContext;
        var sale = await LicenseSalesScope(db)
            .FirstOrDefaultAsync(s => s.Id == saleId, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("License sale not found.");

        var relativePath = Path.Combine(InvoiceStorageRelativePath, $"{sale.InvoiceNumber}.pdf");
        var absolutePath = Path.Combine(_environment.ContentRootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await File.WriteAllBytesAsync(absolutePath, pdf, ct).ConfigureAwait(false);

        if (!string.Equals(sale.InvoicePdfPath, relativePath, StringComparison.Ordinal))
        {
            sale.InvoicePdfPath = relativePath.Replace('\\', '/');
            sale.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return pdf;
    }

    #region Private Methods

    private static IQueryable<LicenseSale> LicenseSalesScope(AppDbContext db) =>
        db.LicenseSales.IgnoreQueryFilters();

    private static (DateTime ValidFrom, DateTime ValidUntil) GetValidityPeriod(
        Tenant tenant,
        string plan,
        DateTime? customValidUntil)
    {
        if (!LicenseSalePlans.IsValid(plan))
            throw new ArgumentException("Invalid license plan.", nameof(plan));

        var normalizedPlan = plan.Trim();
        var now = DateTime.UtcNow;
        var validFrom = tenant.LicenseValidUntilUtc.HasValue && tenant.LicenseValidUntilUtc.Value > now
            ? DateTime.SpecifyKind(tenant.LicenseValidUntilUtc.Value, DateTimeKind.Utc)
            : now;

        if (normalizedPlan == LicenseSalePlans.Custom)
        {
            if (!customValidUntil.HasValue)
                throw new ArgumentException("CustomValidUntilUtc is required for custom license plans.");

            var validUntil = DateTime.SpecifyKind(customValidUntil.Value, DateTimeKind.Utc);
            if (validUntil <= validFrom)
                throw new ArgumentException("CustomValidUntilUtc must be after the license start date.");

            return (validFrom, validUntil);
        }

        var planDef = Plans.FirstOrDefault(p => p.Plan == normalizedPlan)
            ?? throw new ArgumentException("Invalid license plan.", nameof(plan));

        return (validFrom, validFrom.AddDays(planDef.DurationDays));
    }

    private static int ResolveDurationDays(string plan, DateTime validFrom, DateTime validUntil)
    {
        var normalizedPlan = plan.Trim();
        if (normalizedPlan == LicenseSalePlans.Custom)
            return Math.Max(1, (int)Math.Round((validUntil - validFrom).TotalDays));

        var planDef = Plans.FirstOrDefault(p => p.Plan == normalizedPlan);
        return planDef?.DurationDays
            ?? Math.Max(1, (int)Math.Round((validUntil - validFrom).TotalDays));
    }

    private static string GetDurationDisplay(int days, string plan)
    {
        var normalizedPlan = plan.Trim();
        if (normalizedPlan == LicenseSalePlans.Custom)
            return $"{days} Tage (benutzerdefiniert)";

        var planDef = Plans.FirstOrDefault(p => p.Plan == normalizedPlan);
        if (planDef != null && planDef.DurationDays > 0)
            return planDef.DisplayName;

        return $"{days} Tage";
    }

    private static (decimal VatAmount, decimal PriceGross) CalculateAmounts(decimal priceNet, decimal vatRate)
    {
        var vatAmount = Math.Round(priceNet * vatRate / 100m, 2, MidpointRounding.AwayFromZero);
        return (vatAmount, priceNet + vatAmount);
    }

    private static void ValidatePricing(decimal priceNet, decimal vatRate)
    {
        if (priceNet <= 0)
            throw new ArgumentException("PriceNet must be greater than zero.", nameof(priceNet));

        if (vatRate < 0)
            throw new ArgumentException("VatRate cannot be negative.", nameof(vatRate));
    }

    private async Task<string> AllocateInvoiceNumberAsync(
        AppDbContext db,
        DateTime date,
        CancellationToken ct = default)
    {
        var utc = ToUtcInstant(date);
        var year = utc.Year;
        var month = utc.Month;

        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            ct).ConfigureAwait(false);
        try
        {
            var sequence = await db.InvoiceSequences
                .FirstOrDefaultAsync(s => s.Year == year && s.Month == month, ct)
                .ConfigureAwait(false);

            if (sequence == null)
            {
                sequence = new InvoiceSequence
                {
                    Id = Guid.NewGuid(),
                    Year = year,
                    Month = month,
                    LastSequence = 0,
                    UpdatedAt = DateTime.UtcNow,
                };
                db.InvoiceSequences.Add(sequence);
            }

            sequence.LastSequence++;
            sequence.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);

            return FormattableString.Invariant($"RE{year:D4}{month:D2}{sequence.LastSequence}");
        }
        catch
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<bool> IsLicenseKeyAvailableAsync(
        AppDbContext db,
        string licenseKey,
        CancellationToken ct)
    {
        if (!_licenseKeyGenerator.ValidateLicenseKeyFormat(licenseKey))
            return false;

        var key = licenseKey.Trim();
        var hasActiveSale = await LicenseSalesScope(db)
            .AsNoTracking()
            .AnyAsync(s => s.LicenseKey == key && s.Status == LicenseSaleStatuses.Active, ct)
            .ConfigureAwait(false);
        if (hasActiveSale)
            return false;

        var now = DateTime.UtcNow;
        var assignedElsewhere = await db.Tenants
            .AsNoTracking()
            .AnyAsync(
                t => t.LicenseKey == key
                     && t.Status != TenantStatuses.Deleted
                     && (t.LicenseValidUntilUtc == null || t.LicenseValidUntilUtc > now),
                ct)
            .ConfigureAwait(false);

        return !assignedElsewhere;
    }

    private async Task<(string Address, string VatId, string Email)> LoadTenantBillingProfileAsync(
        AppDbContext db,
        Tenant tenant,
        CancellationToken ct)
    {
        var companySettings = await db.CompanySettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenant.Id, ct)
            .ConfigureAwait(false);

        return (
            Address: tenant.Address ?? companySettings?.CompanyAddress ?? string.Empty,
            VatId: companySettings?.CompanyTaxNumber ?? string.Empty,
            Email: tenant.Email ?? companySettings?.CompanyEmail ?? string.Empty);
    }

    private async Task<LicenseSaleResponse> MapToResponseAsync(
        LicenseSale sale,
        AppDbContext db,
        CancellationToken ct)
    {
        var userNames = await LoadUserDisplayNamesAsync(db, [sale], ct).ConfigureAwait(false);
        return MapToResponse(sale, userNames);
    }

    private static LicenseSaleResponse MapToResponse(
        LicenseSale sale,
        IReadOnlyDictionary<Guid, string> userNames)
    {
        return new LicenseSaleResponse
        {
            Id = sale.Id,
            TenantId = sale.TenantId,
            TenantName = sale.Tenant?.Name ?? "Unknown",
            TenantSlug = sale.Tenant?.Slug ?? "Unknown",
            LicenseKey = sale.LicenseKey,
            LicensePlan = sale.LicensePlan,
            ValidFromUtc = sale.ValidFromUtc,
            ValidUntilUtc = sale.ValidUntilUtc,
            PriceNet = sale.PriceNet,
            VatRate = sale.VatRate,
            VatAmount = sale.VatAmount,
            PriceGross = sale.PriceGross,
            Currency = sale.Currency,
            InvoiceNumber = sale.InvoiceNumber,
            InvoicePdfPath = sale.InvoicePdfPath,
            Status = sale.Status,
            SoldAtUtc = sale.SoldAtUtc,
            SoldBy = ResolveUserDisplayName(sale.SoldByUserId, userNames),
            Notes = sale.Notes,
            CancelledAtUtc = sale.CancelledAtUtc,
            CancellationReason = sale.CancellationReason,
            ActivationDateUtc = sale.ActivationDateUtc,
            LastExtendedAtUtc = sale.LastExtendedAtUtc,
            ExtendedBy = sale.ExtendedByUserId.HasValue
                ? ResolveUserDisplayName(sale.ExtendedByUserId.Value, userNames)
                : null,
        };
    }

    private static async Task<IReadOnlyDictionary<Guid, string>> LoadUserDisplayNamesAsync(
        AppDbContext db,
        IReadOnlyList<LicenseSale> sales,
        CancellationToken ct)
    {
        var userIds = sales
            .Select(s => s.SoldByUserId)
            .Concat(sales.Where(s => s.ExtendedByUserId.HasValue).Select(s => s.ExtendedByUserId!.Value))
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
            return new Dictionary<Guid, string>();

        var idStrings = userIds.Select(id => id.ToString("D")).ToList();
        var users = await db.Users
            .AsNoTracking()
            .Where(u => idStrings.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.UserName })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return users.ToDictionary(
            u => Guid.Parse(u.Id),
            u =>
            {
                var name = $"{u.FirstName} {u.LastName}".Trim();
                return string.IsNullOrWhiteSpace(name) ? u.UserName ?? u.Id : name;
            });
    }

    private static string ResolveUserDisplayName(Guid userId, IReadOnlyDictionary<Guid, string> userNames) =>
        userNames.TryGetValue(userId, out var name) ? name : userId.ToString("D");

    private static async Task EnsureUserExistsAsync(AppDbContext db, Guid actorUserId, CancellationToken ct)
    {
        var actorUserIdText = actorUserId.ToString("D");
        var exists = await db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == actorUserIdText, ct)
            .ConfigureAwait(false);
        if (!exists)
            throw new ArgumentException("User not found.");
    }

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

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task ScheduleRemindersForSaleAsync(Guid saleId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<IBillingReminderService>()
            .ScheduleRemindersForSaleAsync(saleId, ct)
            .ConfigureAwait(false);
    }

    private async Task CancelRemindersForSaleAsync(Guid saleId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<IBillingReminderService>()
            .CancelRemindersForSaleAsync(saleId, ct)
            .ConfigureAwait(false);
    }

    private void TriggerSaleBackupIfEnabled(Guid saleId)
    {
        if (!_backupConfig.Enabled || !_backupConfig.BackupOnSaleCreation)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var backupService = scope.ServiceProvider.GetRequiredService<IBillingBackupService>();
                await backupService.BackupSaleAsync(saleId, triggeredByUserId: null, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background backup failed for sale {SaleId}", saleId);
            }
        });
    }

    #endregion
}
