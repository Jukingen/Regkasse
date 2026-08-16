using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Billing;

public sealed class TenantLicenseService : ITenantLicenseService
{
    private readonly AppDbContext _dbContext;
    private readonly IBillingService _billingService;
    private readonly ILicenseKeyGenerator _licenseKeyGenerator;
    private readonly IBillingAuditService _billingAudit;
    private readonly ILicenseStatusCache _licenseStatusCache;
    private readonly ILogger<TenantLicenseService> _logger;

    public TenantLicenseService(
        AppDbContext dbContext,
        IBillingService billingService,
        ILicenseKeyGenerator licenseKeyGenerator,
        IBillingAuditService billingAudit,
        ILicenseStatusCache licenseStatusCache,
        ILogger<TenantLicenseService> logger)
    {
        _dbContext = dbContext;
        _billingService = billingService;
        _licenseKeyGenerator = licenseKeyGenerator;
        _billingAudit = billingAudit;
        _licenseStatusCache = licenseStatusCache;
        _logger = logger;
    }

    /// <summary>
    /// Cache-Aside license status: cache hit → return; miss/expired → load from
    /// <c>license_sales</c> (+ tenant fallback) → populate cache (15 min TTL).
    /// </summary>
    public Task<TenantLicenseStatus> GetCurrentStatusAsync(
        Guid tenantId,
        CancellationToken ct = default) =>
        _licenseStatusCache.GetOrCreateAsync(tenantId, token => LoadCurrentStatusFromDbAsync(tenantId, token), ct);

    /// <summary>
    /// Loads the latest mandant license status from <c>license_sales</c> (authoritative),
    /// falling back to denormalized <see cref="Tenant"/> columns when no sale row exists.
    /// </summary>
    private async Task<TenantLicenseStatus> LoadCurrentStatusFromDbAsync(
        Guid tenantId,
        CancellationToken ct)
    {
        var db = _dbContext;

        var tenant = await db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new
            {
                t.Id,
                t.CurrentLicenseSaleId,
                t.LicenseKey,
                t.LicenseValidUntilUtc,
            })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Tenant {tenantId} not found");

        var now = DateTime.UtcNow;

        LicenseSale? sale = null;
        if (tenant.CurrentLicenseSaleId.HasValue)
        {
            sale = await db.LicenseSales
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == tenant.CurrentLicenseSaleId.Value, ct)
                .ConfigureAwait(false);
        }

        if (sale is not null
            && string.Equals(sale.Status, LicenseSaleStatuses.Active, StringComparison.Ordinal))
        {
            return BuildStatusFromSale(sale, now);
        }

        // Pointer missing/stale/cancelled: use newest active sale for this tenant.
        var latestActive = await db.LicenseSales
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.Status == LicenseSaleStatuses.Active)
            .OrderByDescending(s => s.ValidUntilUtc)
            .ThenByDescending(s => s.SoldAtUtc)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (latestActive is not null)
            return BuildStatusFromSale(latestActive, now);

        // Legacy / denormalized tenant columns when no active license_sales row exists.
        return BuildStatusFromTenantFields(tenant.LicenseKey, tenant.LicenseValidUntilUtc, now);
    }

    private static TenantLicenseStatus BuildStatusFromSale(LicenseSale sale, DateTime nowUtc) =>
        BuildStatusFromValidity(sale.LicenseKey, sale.ValidUntilUtc, sale.LicensePlan, nowUtc);

    private static TenantLicenseStatus BuildStatusFromTenantFields(
        string? licenseKey,
        DateTime? validUntilUtc,
        DateTime nowUtc) =>
        BuildStatusFromValidity(licenseKey, validUntilUtc, licensePlan: null, nowUtc);

    private static TenantLicenseStatus BuildStatusFromValidity(
        string? licenseKey,
        DateTime? validUntilUtc,
        string? licensePlan,
        DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(licenseKey))
        {
            return new TenantLicenseStatus
            {
                LicenseKey = licenseKey,
                ValidUntilUtc = validUntilUtc,
                LicensePlan = licensePlan,
                Status = "none",
                IsValid = false,
            };
        }

        string statusLabel;
        bool isValid;
        int? daysRemaining = null;
        var isExpiringSoon = false;
        var isTrial = false;

        if (validUntilUtc.HasValue)
        {
            daysRemaining = (validUntilUtc.Value - nowUtc).Days;
            isExpiringSoon = daysRemaining <= 30 && daysRemaining > 0;

            if (validUntilUtc.Value <= nowUtc)
            {
                statusLabel = "expired";
                isValid = false;
            }
            else
            {
                statusLabel = "valid";
                isValid = true;
                isTrial = validUntilUtc.Value <= nowUtc.AddMonths(1);
            }
        }
        else
        {
            statusLabel = "expired";
            isValid = false;
        }

        return new TenantLicenseStatus
        {
            LicenseKey = licenseKey,
            ValidUntilUtc = validUntilUtc,
            Status = statusLabel,
            IsValid = isValid,
            DaysRemaining = daysRemaining,
            IsExpiringSoon = isExpiringSoon,
            IsTrial = isTrial,
            LicensePlan = licensePlan,
        };
    }

    private Task InvalidateLicenseCacheAsync(Guid tenantId, CancellationToken ct = default) =>
        _licenseStatusCache.InvalidateLicenseCacheAsync(tenantId, ct);

    public async Task<ActivationResult> ActivateLicenseAsync(
        Guid tenantId,
        string licenseKey,
        Guid activatedByUserId,
        CancellationToken ct = default)
    {
        var db = _dbContext;
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            if (!_licenseKeyGenerator.ValidateLicenseKeyFormat(licenseKey)
                && !_licenseKeyGenerator.ValidateLicenseKeyFormat(
                    await ResolveCanonicalLicenseKeyAsync(licenseKey, ct).ConfigureAwait(false)))
            {
                return new ActivationResult
                {
                    Success = false,
                    Message = "Ungültiges Lizenzformat. Bitte überprüfen Sie den Schlüssel.",
                };
            }

            var normalizedKey = licenseKey.Trim();
            var lookupKey = await ResolveCanonicalLicenseKeyAsync(normalizedKey, ct).ConfigureAwait(false);
            var sale = await db.LicenseSales
                .IgnoreQueryFilters()
                .Include(l => l.Tenant)
                .FirstOrDefaultAsync(l => l.LicenseKey == lookupKey || l.LicenseKey == normalizedKey, ct)
                .ConfigureAwait(false);

            if (sale == null)
            {
                return new ActivationResult
                {
                    Success = false,
                    Message = "Lizenzschlüssel nicht gefunden. Bitte überprüfen Sie die Eingabe.",
                };
            }

            if (!string.Equals(sale.Status, LicenseSaleStatuses.Active, StringComparison.Ordinal))
            {
                return new ActivationResult
                {
                    Success = false,
                    Message = string.Equals(sale.Status, LicenseSaleStatuses.Cancelled, StringComparison.Ordinal)
                        ? "Diese Lizenz wurde storniert."
                        : "Diese Lizenz ist nicht mehr gültig.",
                };
            }

            if (sale.ValidUntilUtc <= DateTime.UtcNow)
            {
                return new ActivationResult
                {
                    Success = false,
                    Message = "Diese Lizenz ist bereits abgelaufen.",
                };
            }

            if (sale.TenantId != tenantId)
            {
                var parsed = _licenseKeyGenerator.ParseLicenseKey(normalizedKey);
                if (parsed.TenantSlug != null)
                {
                    var slugTenant = await db.Tenants
                        .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
                        .ConfigureAwait(false);

                    if (slugTenant != null
                        && !string.Equals(slugTenant.Slug, parsed.TenantSlug, StringComparison.OrdinalIgnoreCase))
                    {
                        return new ActivationResult
                        {
                            Success = false,
                            Message = "Dieser Lizenzschlüssel ist für einen anderen Mandanten ausgestellt.",
                        };
                    }
                }

                sale.TenantId = tenantId;
                sale.UpdatedAt = DateTime.UtcNow;
            }

            var tenant = await db.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"Tenant {tenantId} not found");

            var now = DateTime.UtcNow;
            tenant.CurrentLicenseSaleId = sale.Id;
            tenant.LicenseKey = sale.LicenseKey;
            tenant.LicenseValidUntilUtc = sale.ValidUntilUtc;
            tenant.LastLicenseActivationUtc = now;
            tenant.LicenseActivationCount++;
            tenant.UpdatedAt = now;

            sale.ActivationDateUtc = now;
            sale.UpdatedAt = now;

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);

            await InvalidateLicenseCacheAsync(tenantId, ct).ConfigureAwait(false);

            await _billingAudit
                .LogLicenseActivatedAsync(sale, activatedByUserId, ipAddress: null, cancellationToken: ct)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "License activated for tenant {TenantSlug}: {LicenseKey}",
                tenant.Slug,
                normalizedKey);

            var saleResponse = await _billingService.GetLicenseSaleAsync(sale.Id, ct).ConfigureAwait(false);

            return new ActivationResult
            {
                Success = true,
                Message = "Lizenz wurde erfolgreich aktiviert.",
                LicenseKey = sale.LicenseKey,
                ValidUntilUtc = sale.ValidUntilUtc,
                LicensePlan = sale.LicensePlan,
                Sale = saleResponse,
            };
        }
        catch
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<bool> IsLicenseValidAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        // Always goes through Cache-Aside GetCurrentStatusAsync.
        var status = await GetCurrentStatusAsync(tenantId, ct).ConfigureAwait(false);
        return status.IsValid;
    }

    public async Task<TenantLicenseInfo> GetLicenseInfoAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var db = _dbContext;

        // Status is Cache-Aside (cache → license_sales on miss).
        var status = await GetCurrentStatusAsync(tenantId, ct).ConfigureAwait(false);

        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Tenant {tenantId} not found");

        LicenseSaleResponse? currentSale = null;
        if (tenant.CurrentLicenseSaleId.HasValue)
        {
            try
            {
                currentSale = await _billingService
                    .GetLicenseSaleAsync(tenant.CurrentLicenseSaleId.Value, ct)
                    .ConfigureAwait(false);
            }
            catch (KeyNotFoundException)
            {
                // Sale not found, ignore
            }
        }

        if (currentSale is null && !string.IsNullOrEmpty(status.LicenseKey))
        {
            currentSale = await _billingService
                .GetSaleByLicenseKeyAsync(status.LicenseKey, ct)
                .ConfigureAwait(false);
        }

        var history = await _billingService.ListLicenseSalesAsync(
            new LicenseSaleListQuery
            {
                TenantId = tenantId,
                PageSize = 100,
            },
            ct).ConfigureAwait(false);

        return new TenantLicenseInfo
        {
            Status = status,
            CurrentSale = currentSale,
            History = history.Items,
            LastActivationUtc = tenant.LastLicenseActivationUtc,
            ActivationCount = tenant.LicenseActivationCount,
        };
    }

    public async Task<List<LicenseSaleResponse>> GetLicenseHistoryAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var result = await _billingService.ListLicenseSalesAsync(
            new LicenseSaleListQuery
            {
                TenantId = tenantId,
                PageSize = 100,
            },
            ct).ConfigureAwait(false);

        return result.Items;
    }

    public async Task<ExtendResult> ExtendLicenseAsync(
        Guid tenantId,
        string licenseKey,
        Guid extendedByUserId,
        CancellationToken ct = default)
    {
        var activationResult = await ActivateLicenseAsync(
            tenantId,
            licenseKey,
            extendedByUserId,
            ct).ConfigureAwait(false);

        if (!activationResult.Success)
        {
            return new ExtendResult
            {
                Success = false,
                Message = activationResult.Message,
            };
        }

        var db = _dbContext;

        var normalizedKey = licenseKey.Trim();
        var sale = await db.LicenseSales
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.LicenseKey == normalizedKey, ct)
            .ConfigureAwait(false);

        if (sale != null)
        {
            var now = DateTime.UtcNow;
            sale.LastExtendedAtUtc = now;
            sale.ExtendedByUserId = extendedByUserId;
            sale.UpdatedAt = now;

            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            await InvalidateLicenseCacheAsync(tenantId, ct).ConfigureAwait(false);

            await _billingAudit
                .LogLicenseExtendedAsync(sale, extendedByUserId, ipAddress: null, cancellationToken: ct)
                .ConfigureAwait(false);
        }

        return new ExtendResult
        {
            Success = true,
            Message = "Lizenz wurde erfolgreich verlängert.",
            LicenseKey = activationResult.LicenseKey,
            ValidUntilUtc = activationResult.ValidUntilUtc,
            LicensePlan = activationResult.LicensePlan,
            Sale = activationResult.Sale,
        };
    }

    public async Task<List<ExpiringLicenseInfo>> GetExpiringLicensesAsync(
        int daysThreshold = 30,
        CancellationToken ct = default)
    {
        var db = _dbContext;

        var now = DateTime.UtcNow;
        var thresholdDate = now.AddDays(daysThreshold);

        var expiringSales = await db.LicenseSales
            .IgnoreQueryFilters()
            .Include(l => l.Tenant)
            .Where(l => l.Status == LicenseSaleStatuses.Active
                        && l.ValidUntilUtc > now
                        && l.ValidUntilUtc <= thresholdDate)
            .OrderBy(l => l.ValidUntilUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return expiringSales.Select(sale => new ExpiringLicenseInfo
        {
            TenantId = sale.TenantId,
            TenantName = sale.Tenant?.Name ?? "Unknown",
            TenantSlug = sale.Tenant?.Slug ?? "Unknown",
            LicenseKey = sale.LicenseKey,
            ValidUntilUtc = sale.ValidUntilUtc,
            DaysRemaining = (sale.ValidUntilUtc - now).Days,
            LicenseSaleId = sale.Id,
            TenantEmail = sale.Tenant?.Email,
        }).ToList();
    }

    private async Task<string> ResolveCanonicalLicenseKeyAsync(string licenseKey, CancellationToken ct)
    {
        var trimmed = licenseKey.Trim();
        var mapped = await _dbContext.LicenseKeyMappings.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(m => m.OldLicenseKey == trimmed || m.NewLicenseKey == trimmed)
            .Select(m => m.NewLicenseKey)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return string.IsNullOrEmpty(mapped) ? trimmed : mapped;
    }
}
