using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.AdminTenants;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tenancy;

public sealed class TenantHardDeletePolicy : ITenantHardDeletePolicy
{
    private readonly IHostEnvironment _environment;
    private readonly TenantDeletionOptions _options;

    public TenantHardDeletePolicy(
        IHostEnvironment environment,
        IOptions<TenantDeletionOptions> options)
    {
        _environment = environment;
        _options = options?.Value ?? new TenantDeletionOptions();
    }

    public bool IsProduction() =>
        !_environment.IsDevelopment() && !_options.AllowPermanentDeleteInProduction;

    public bool HasFiscalFootprint(TenantDeleteDependencyCountsDto counts) =>
        counts.Payments > 0 || counts.DailyClosings > 0;

    public IReadOnlyList<TenantDeleteDependencyBlockerDto> GetBlockers(
        TenantDeleteDependencyCountsDto counts,
        bool isProduction)
    {
        var blockers = new List<TenantDeleteDependencyBlockerDto>();

        if (isProduction && HasAnyDependency(counts))
        {
            blockers.Add(new TenantDeleteDependencyBlockerDto(
                TenantPermanentDeleteFailureCodes.ProductionPolicy,
                DependencyTotal(counts),
                "blocking",
                "Permanent tenant deletion is disabled in production when dependencies exist."));
        }

        if (HasFiscalFootprint(counts))
        {
            blockers.Add(new TenantDeleteDependencyBlockerDto(
                TenantPermanentDeleteFailureCodes.FiscalFootprintPresent,
                Math.Max(counts.Payments, counts.DailyClosings),
                "compliance",
                "Tenant has fiscal footprint; keep soft-deleted for RKSV retention."));
        }

        if (counts.CashRegisters > 0)
        {
            blockers.Add(new TenantDeleteDependencyBlockerDto(
                TenantPermanentDeleteFailureCodes.CashRegistersPresent,
                counts.CashRegisters,
                "blocking",
                "Remove cash registers before permanent delete."));
        }

        return blockers;
    }

    public (bool CanDelete, string? FailureCode, string? FailureMessage) Validate(
        TenantDeleteDependencyCountsDto counts,
        bool isProduction,
        bool forceDelete)
    {
        if (forceDelete && isProduction)
        {
            return (
                false,
                TenantPermanentDeleteFailureCodes.ForceDeleteDevelopmentOnly,
                "Force delete is only allowed in Development.");
        }

        if (isProduction && HasAnyDependency(counts))
        {
            return (
                false,
                TenantPermanentDeleteFailureCodes.ProductionPolicy,
                "Permanent tenant deletion is disabled in production when dependencies exist.");
        }

        if (isProduction)
        {
            return (
                false,
                TenantPermanentDeleteFailureCodes.ProductionPolicy,
                "Permanent tenant deletion is disabled in production. Use soft-delete to archive the tenant.");
        }

        if (HasFiscalFootprint(counts))
        {
            return (
                false,
                TenantPermanentDeleteFailureCodes.FiscalFootprintPresent,
                "Cannot permanently delete tenant with fiscal records. Keep the soft-deleted tenant for compliance.");
        }

        if (counts.CashRegisters > 0)
        {
            return (
                false,
                TenantPermanentDeleteFailureCodes.CashRegistersPresent,
                "Cannot permanently delete tenant with cash registers. Remove registers first.");
        }

        return (true, null, null);
    }

    private static bool HasAnyDependency(TenantDeleteDependencyCountsDto counts) =>
        counts.Users > 0
        || counts.Memberships > 0
        || counts.CashRegisters > 0
        || counts.Payments > 0
        || counts.Receipts > 0
        || counts.Vouchers > 0
        || counts.VoucherLedgerEntries > 0
        || counts.DailyClosings > 0
        || counts.Products > 0
        || counts.Categories > 0
        || counts.AuditLogs > 0
        || counts.FinanzOnlineSubmissions > 0;

    private static int DependencyTotal(TenantDeleteDependencyCountsDto counts) =>
        counts.Users
        + counts.Memberships
        + counts.CashRegisters
        + counts.Payments
        + counts.Receipts
        + counts.Vouchers
        + counts.VoucherLedgerEntries
        + counts.DailyClosings
        + counts.Products
        + counts.Categories
        + counts.AuditLogs
        + counts.FinanzOnlineSubmissions;
}
