using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tenancy;

public sealed partial class TenantDeletionService : ITenantDeletionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITenantHardDeletePolicy _policy;

    public TenantDeletionService(
        IServiceScopeFactory scopeFactory,
        ITenantHardDeletePolicy policy)
    {
        _scopeFactory = scopeFactory;
        _policy = policy;
    }

    public Task<TenantDeleteDependenciesDto> GetDependencySummaryAsync(
        Guid tenantId,
        CancellationToken ct = default) =>
        WithDbAsync(
            async (db, cancellationToken) =>
            {
                var tenant = await FindTenantAsync(db, tenantId, cancellationToken).ConfigureAwait(false)
                             ?? throw new KeyNotFoundException("Tenant not found.");

                return await BuildDependenciesAsync(db, tenant, cancellationToken).ConfigureAwait(false);
            },
            ct);

    public Task<(bool Success, string? ErrorCode, string? ErrorMessage)> ValidateHardDeleteAsync(
        Guid tenantId,
        bool forceDelete = false,
        CancellationToken ct = default)
    {
        if (tenantId == LegacyDefaultTenantIds.Primary)
        {
            return Task.FromResult<(bool, string?, string?)>((
                false,
                TenantPermanentDeleteFailureCodes.LegacyDefaultTenant,
                "The legacy default tenant cannot be permanently deleted."));
        }

        return WithDbAsync<(bool Success, string? ErrorCode, string? ErrorMessage)>(
            async (db, cancellationToken) =>
            {
                var tenant = await FindTenantAsync(db, tenantId, cancellationToken).ConfigureAwait(false);
                if (tenant == null)
                {
                    return (
                        false,
                        TenantPermanentDeleteFailureCodes.TenantNotFound,
                        "Tenant not found.");
                }

                if (string.Equals(tenant.Slug, LegacyDefaultTenantIds.PrimarySlug, StringComparison.Ordinal))
                {
                    return (
                        false,
                        TenantPermanentDeleteFailureCodes.LegacyDefaultTenant,
                        "The legacy default tenant cannot be permanently deleted.");
                }

                if (tenant.Status != TenantStatuses.Deleted)
                {
                    return (
                        false,
                        TenantPermanentDeleteFailureCodes.NotSoftDeleted,
                        "Tenant must be soft-deleted before permanent deletion.");
                }

                var counts = await GetCountsAsync(db, tenantId, cancellationToken).ConfigureAwait(false);
                var validation = _policy.Validate(counts, _policy.IsProduction(), forceDelete);
                if (!validation.CanDelete)
                {
                    return (false, validation.FailureCode, validation.FailureMessage);
                }

                return (true, null, null);
            },
            ct);
    }

    private async Task<T> WithDbAsync<T>(
        Func<AppDbContext, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await action(db, cancellationToken).ConfigureAwait(false);
    }

    private static Task<Tenant?> FindTenantAsync(
        AppDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

    private async Task<TenantDeleteDependenciesDto> BuildDependenciesAsync(
        AppDbContext db,
        Tenant tenant,
        CancellationToken cancellationToken)
    {
        var tenantId = tenant.Id;
        var counts = await GetCountsAsync(db, tenantId, cancellationToken).ConfigureAwait(false);
        var isProduction = _policy.IsProduction();
        var hasFiscalFootprint = _policy.HasFiscalFootprint(counts);
        var policyBlockers = _policy.GetBlockers(counts, isProduction);
        var blockers = BuildBlockers(tenant, counts, policyBlockers);
        var nextSteps = BuildNextSteps(tenant, counts, hasFiscalFootprint, blockers);

        string? failureCode = null;
        string? failureMessage = null;

        if (tenant.Id == LegacyDefaultTenantIds.Primary
            || string.Equals(tenant.Slug, LegacyDefaultTenantIds.PrimarySlug, StringComparison.Ordinal))
        {
            failureCode = TenantPermanentDeleteFailureCodes.LegacyDefaultTenant;
            failureMessage = "The legacy default tenant cannot be permanently deleted.";
        }
        else if (tenant.Status != TenantStatuses.Deleted)
        {
            failureCode = TenantPermanentDeleteFailureCodes.NotSoftDeleted;
            failureMessage = "Tenant must be soft-deleted before permanent deletion.";
        }
        else
        {
            var validation = _policy.Validate(counts, isProduction, forceDelete: false);
            if (!validation.CanDelete)
            {
                failureCode = validation.FailureCode;
                failureMessage = validation.FailureMessage;
            }
        }

        var canHardDelete = failureCode == null;

        var hasDependencies = blockers.Count > 0
                              || counts.Memberships > 0
                              || counts.Products > 0
                              || counts.Categories > 0
                              || hasFiscalFootprint;

        return new TenantDeleteDependenciesDto(
            tenantId,
            tenant.Slug,
            tenant.Status,
            canHardDelete,
            hasDependencies,
            hasFiscalFootprint,
            failureCode,
            failureMessage,
            counts,
            blockers,
            nextSteps);
    }

    private static List<TenantDeleteDependencyBlockerDto> BuildBlockers(
        Tenant tenant,
        TenantDeleteDependencyCountsDto counts,
        IReadOnlyList<TenantDeleteDependencyBlockerDto> policyBlockers)
    {
        var blockers = new List<TenantDeleteDependencyBlockerDto>(policyBlockers);

        if (tenant.Status != TenantStatuses.Deleted)
        {
            blockers.Add(new TenantDeleteDependencyBlockerDto(
                TenantPermanentDeleteFailureCodes.NotSoftDeleted,
                1,
                "blocking",
                "Soft-delete (archive) the tenant before permanent delete."));
        }

        if (counts.Memberships > 0)
        {
            blockers.Add(new TenantDeleteDependencyBlockerDto(
                "memberships",
                counts.Memberships,
                "info",
                "User memberships will be removed on permanent delete."));
        }

        if (counts.Products > 0 || counts.Categories > 0)
        {
            blockers.Add(new TenantDeleteDependencyBlockerDto(
                "catalog",
                counts.Products + counts.Categories,
                "info",
                "Products and categories will be removed on permanent delete."));
        }

        if (counts.AuditLogs > 0)
        {
            blockers.Add(new TenantDeleteDependencyBlockerDto(
                "audit_logs",
                counts.AuditLogs,
                "compliance",
                "Audit logs remain tenant-scoped; permanent delete does not purge audit history."));
        }

        return blockers;
    }

    private static List<string> BuildNextSteps(
        Tenant tenant,
        TenantDeleteDependencyCountsDto counts,
        bool hasFiscalFootprint,
        IReadOnlyList<TenantDeleteDependencyBlockerDto> blockers)
    {
        var steps = new List<string>();

        if (tenant.Status != TenantStatuses.Deleted)
            steps.Add("soft_delete_archive");

        if (hasFiscalFootprint || counts.Payments > 0)
            steps.Add("compliance_soft_delete_only");

        if (counts.CashRegisters > 0)
            steps.Add("remove_cash_registers");

        if (counts.Memberships > 0 || counts.Users > 0)
            steps.Add("review_tenant_users");

        if (counts.Payments > 0 || counts.Receipts > 0)
            steps.Add("review_fiscal_records");

        if (blockers.Any(b => b.Severity == "compliance"))
            steps.Add("retain_for_rksv_retention");

        if (steps.Count == 0 && tenant.Status == TenantStatuses.Deleted)
            steps.Add("eligible_for_dev_permanent_delete");

        return steps;
    }
}
