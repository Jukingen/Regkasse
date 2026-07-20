using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.DataRetention;

/// <summary>
/// RKSV §7-style retention reporting (7 years). Does not delete fiscal rows —
/// customer-data purge only removes non-RKSV business data.
/// </summary>
public sealed class RksvDataRetentionService : IRksvDataRetentionService
{
    public const int RetentionYears = 7;

    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public RksvDataRetentionService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<RetentionReport> GetRetentionStatusAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var tenantExists = await db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId, ct)
            .ConfigureAwait(false);
        if (!tenantExists)
            throw new InvalidOperationException($"Tenant {tenantId} not found.");

        var now = DateTime.UtcNow;
        var cutoff = now.AddYears(-RetentionYears);

        var cashRegisterIds = await db.CashRegisters.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var paymentQuery = db.PaymentDetails.AsNoTracking()
            .Where(p => cashRegisterIds.Contains(p.CashRegisterId));

        var paymentCount = await paymentQuery.CountAsync(ct).ConfigureAwait(false);
        var pastRetentionCount = await paymentQuery
            .CountAsync(p => p.CreatedAt < cutoff, ct)
            .ConfigureAwait(false);

        DateTime? oldestPayment = null;
        if (paymentCount > 0)
        {
            oldestPayment = await paymentQuery
                .OrderBy(p => p.CreatedAt)
                .Select(p => (DateTime?)p.CreatedAt)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }

        var receiptsQuery = db.Receipts.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId);
        var receiptsCount = await receiptsQuery.CountAsync(ct).ConfigureAwait(false);
        DateTime? oldestReceipt = null;
        if (receiptsCount > 0)
        {
            oldestReceipt = await receiptsQuery
                .OrderBy(r => r.IssuedAt)
                .Select(r => (DateTime?)r.IssuedAt)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }

        var dailyClosingsCount = await db.DailyClosings.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(x => x.TenantId == tenantId, ct)
            .ConfigureAwait(false);
        var auditLogsCount = await db.AuditLogs.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(x => x.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        DateTime? retentionUntil = oldestPayment?.AddYears(RetentionYears);
        DateTime? willBeDeletedOn = retentionUntil?.AddDays(1);
        var underObligation = paymentCount > 0
            && (oldestPayment is null || oldestPayment.Value.AddYears(RetentionYears) > now);

        var productsCount = await db.Products.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(p => p.TenantId == tenantId, ct)
            .ConfigureAwait(false);
        var customersCount = await db.Customers.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(c => c.TenantId == tenantId, ct)
            .ConfigureAwait(false);
        var categoriesCount = await db.Categories.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(c => c.TenantId == tenantId, ct)
            .ConfigureAwait(false);
        var invoicesCount = await db.Invoices.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(i => i.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        var purged = await db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => t.CustomerDataPurgedAtUtc)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return new RetentionReport
        {
            TenantId = tenantId,
            RetentionYears = RetentionYears,
            AsOfUtc = now,
            RetentionCutoffUtc = cutoff,
            RksvData = new RksvDataStatus
            {
                PaymentDetailsCount = paymentCount,
                PaymentDetailsPastRetentionCount = pastRetentionCount,
                ReceiptsCount = receiptsCount,
                DailyClosingsCount = dailyClosingsCount,
                AuditLogsCount = auditLogsCount,
                OldestPaymentDate = oldestPayment,
                OldestReceiptDate = oldestReceipt,
                RetentionUntil = retentionUntil,
                WillBeDeletedOn = willBeDeletedOn,
                IsUnderRetentionObligation = underObligation,
            },
            NonRksvData = new NonRksvDataStatus
            {
                ProductsCount = productsCount,
                CustomersCount = customersCount,
                CategoriesCount = categoriesCount,
                InvoicesCount = invoicesCount,
                CanBeDeleted = !purged.HasValue,
            },
        };
    }
}
