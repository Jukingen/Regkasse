using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Billing;

public sealed class TenantLicenseService : ITenantLicenseService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IBillingService _billingService;
    private readonly ILicenseKeyGenerator _licenseKeyGenerator;
    private readonly IBillingAuditService _billingAudit;
    private readonly ILogger<TenantLicenseService> _logger;

    public TenantLicenseService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IBillingService billingService,
        ILicenseKeyGenerator licenseKeyGenerator,
        IBillingAuditService billingAudit,
        ILogger<TenantLicenseService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _billingService = billingService;
        _licenseKeyGenerator = licenseKeyGenerator;
        _billingAudit = billingAudit;
        _logger = logger;
    }

    public async Task<TenantLicenseStatus> GetCurrentStatusAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Tenant {tenantId} not found");

        var now = DateTime.UtcNow;
        string statusLabel;
        bool isValid;
        int? daysRemaining = null;
        bool isExpiringSoon = false;
        bool isTrial = false;

        if (string.IsNullOrEmpty(tenant.LicenseKey))
        {
            return new TenantLicenseStatus
            {
                LicenseKey = tenant.LicenseKey,
                ValidUntilUtc = tenant.LicenseValidUntilUtc,
                Status = "none",
                IsValid = false,
            };
        }

        if (tenant.LicenseValidUntilUtc.HasValue)
        {
            daysRemaining = (tenant.LicenseValidUntilUtc.Value - now).Days;
            isExpiringSoon = daysRemaining <= 30 && daysRemaining > 0;

            if (tenant.LicenseValidUntilUtc.Value <= now)
            {
                statusLabel = "expired";
                isValid = false;
            }
            else
            {
                statusLabel = "valid";
                isValid = true;
                isTrial = tenant.LicenseValidUntilUtc.Value <= now.AddMonths(1);
            }
        }
        else
        {
            statusLabel = "expired";
            isValid = false;
        }

        string? licensePlan = null;
        if (tenant.CurrentLicenseSaleId.HasValue)
        {
            licensePlan = await db.LicenseSales
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => s.Id == tenant.CurrentLicenseSaleId.Value)
                .Select(s => s.LicensePlan)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }

        return new TenantLicenseStatus
        {
            LicenseKey = tenant.LicenseKey,
            ValidUntilUtc = tenant.LicenseValidUntilUtc,
            Status = statusLabel,
            IsValid = isValid,
            DaysRemaining = daysRemaining,
            IsExpiringSoon = isExpiringSoon,
            IsTrial = isTrial,
            LicensePlan = licensePlan,
        };
    }

    public async Task<ActivationResult> ActivateLicenseAsync(
        Guid tenantId,
        string licenseKey,
        Guid activatedByUserId,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            if (!_licenseKeyGenerator.ValidateLicenseKeyFormat(licenseKey))
            {
                return new ActivationResult
                {
                    Success = false,
                    Message = "Ungültiges Lizenzformat. Bitte überprüfen Sie den Schlüssel.",
                };
            }

            var normalizedKey = licenseKey.Trim();
            var sale = await db.LicenseSales
                .IgnoreQueryFilters()
                .Include(l => l.Tenant)
                .FirstOrDefaultAsync(l => l.LicenseKey == normalizedKey, ct)
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
            tenant.LicenseKey = normalizedKey;
            tenant.LicenseValidUntilUtc = sale.ValidUntilUtc;
            tenant.LastLicenseActivationUtc = now;
            tenant.LicenseActivationCount++;
            tenant.UpdatedAt = now;

            sale.ActivationDateUtc = now;
            sale.UpdatedAt = now;

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);

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
                LicenseKey = normalizedKey,
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
        var status = await GetCurrentStatusAsync(tenantId, ct).ConfigureAwait(false);
        return status.IsValid;
    }

    public async Task<TenantLicenseInfo> GetLicenseInfoAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Tenant {tenantId} not found");

        var status = await GetCurrentStatusAsync(tenantId, ct).ConfigureAwait(false);

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

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

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
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

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
}
