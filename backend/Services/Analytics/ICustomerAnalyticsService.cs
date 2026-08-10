using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Analytics;

/// <summary>Super Admin customer / mandant KPI snapshot.</summary>
public sealed record CustomerAnalyticsDto(
    int TotalTenants,
    int ActiveTenants,
    int InOnboardingTenants,
    int SuspendedTenants,
    int TrialTenants,
    int PaidTenants,
    int ExpiringSoon,
    int ExpiredTenants,
    decimal Mrr,
    int NewTenantsLast30Days);

public interface ICustomerAnalyticsService
{
    Task<CustomerAnalyticsDto> GetCustomerAnalyticsAsync(CancellationToken cancellationToken = default);
}
