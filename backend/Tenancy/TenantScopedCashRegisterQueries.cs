using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Tenant-scoped entities inherit the ambient tenant query filter from <see cref="ICurrentTenantAccessor"/>.
/// Queries that already resolved effective tenant via <see cref="ISettingsTenantResolver"/> must bypass
/// that filter and apply explicit <c>TenantId</c> predicates instead.
/// </summary>
public static class TenantScopedEntityQueries
{
    public static IQueryable<TEntity> ForResolvedTenantScope<TEntity>(this IQueryable<TEntity> query)
        where TEntity : class, ITenantEntity =>
        query.IgnoreQueryFilters();

    public static IQueryable<CashRegister> ForResolvedTenantScope(this IQueryable<CashRegister> query) =>
        query.IgnoreQueryFilters();
}
