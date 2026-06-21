using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.AdminTenants;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tenancy;

public sealed partial class TenantDeletionService
{
    private static async Task<TenantDeleteDependencyCountsDto> GetCountsAsync(
        AppDbContext db,
        Guid tenantId,
        CancellationToken ct)
    {
        var membershipsQuery = db.Set<UserTenantMembership>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId);

        var users = await membershipsQuery
            .Select(m => m.UserId)
            .Distinct()
            .CountAsync(ct)
            .ConfigureAwait(false);

        var memberships = await membershipsQuery.CountAsync(ct).ConfigureAwait(false);

        var cashRegisters = await db.Set<CashRegister>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(r => r.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        var payments = await db.Set<PaymentDetails>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(p => p.CashRegister != null && p.CashRegister.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        var receipts = await db.Set<Receipt>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(r => r.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        var vouchers = await db.Set<Voucher>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(v => v.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        var voucherLedgerEntries = await db.Set<VoucherLedgerEntry>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(v => v.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        var dailyClosings = await db.Set<DailyClosing>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(d => d.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        var products = await db.Set<Product>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(p => p.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        var categories = await db.Set<Category>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(c => c.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        var auditLogs = await db.Set<AuditLog>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(a => a.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        var finanzOnlineSubmissions = await db.Set<FinanzOnlineSubmission>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(f => f.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        return new TenantDeleteDependencyCountsDto(
            users,
            memberships,
            cashRegisters,
            payments,
            receipts,
            vouchers,
            voucherLedgerEntries,
            dailyClosings,
            products,
            categories,
            auditLogs,
            finanzOnlineSubmissions);
    }
}
