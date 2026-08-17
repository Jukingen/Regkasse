using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Analytics;

/// <summary>Paid-package mix on the current active license sale (latest ValidUntilUtc per tenant).</summary>
public sealed record PlanDistributionDto(
    int Trial,
    int Starter,
    int Business,
    int Plus);

/// <summary>Super Admin customer / mandant KPI snapshot (SaaS — not POS fiscal revenue).</summary>
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
    int NewTenantsLast30Days,
    decimal ChurnRate,
    decimal Arpu,
    PlanDistributionDto PlanDistribution,
    decimal? CustomerLtv);

public interface ICustomerAnalyticsService
{
    Task<CustomerAnalyticsDto> GetCustomerAnalyticsAsync(CancellationToken cancellationToken = default);
}
