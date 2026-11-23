using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KasseAPI_Final.Services;

/// <summary>
/// Runs audit and FinanzOnline after the fiscal commit, on a new scope.
/// The sale context's connection goes back to the pool first. Those writes stay best-effort and keep their order.
/// </summary>
internal static class PaymentPostCommitDispatcher
{
    public static async Task<PaymentResult> DispatchAfterFiscalCommitAsync(
        AppDbContext saleContext,
        IPaymentPostCommitService requestScopePostCommit,
        IServiceScopeFactory? postCommitScopes,
        ICurrentTenantAccessor? requestTenant,
        Func<IPaymentPostCommitService, Task<PaymentResult>> dispatch)
    {
        if (postCommitScopes == null)
            return await dispatch(requestScopePostCommit).ConfigureAwait(false);

        // In-memory tests have no pool. Relational hosts return the sale connection before audit opens another.
        if (saleContext.Database.IsRelational())
            await saleContext.Database.CloseConnectionAsync().ConfigureAwait(false);

        await using var scope = postCommitScopes.CreateAsyncScope();
        var scopedTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenantAccessor>();
        if (requestTenant != null)
        {
            scopedTenant.TenantId = requestTenant.TenantId;
            scopedTenant.TenantSlug = requestTenant.TenantSlug;
        }

        var scopedPostCommit = scope.ServiceProvider.GetRequiredService<IPaymentPostCommitService>();
        return await dispatch(scopedPostCommit).ConfigureAwait(false);
    }
}
