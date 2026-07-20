using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Billing;

public interface ISubscriptionService
{
    Task<SubscriptionResult> CreateSubscriptionAsync(
        Guid tenantId,
        string serviceId,
        string? actorUserId = null,
        CancellationToken ct = default);

    Task<SubscriptionResult> CancelSubscriptionAsync(
        Guid subscriptionId,
        string? actorUserId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<Subscription>> ListForTenantAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task<Subscription?> GetByIdAsync(
        Guid subscriptionId,
        CancellationToken ct = default);

    /// <summary>Cross-tenant digital MRR dashboard for Super Admin.</summary>
    Task<DigitalBillingDashboardDto> GetDigitalBillingDashboardAsync(
        CancellationToken ct = default);
}
