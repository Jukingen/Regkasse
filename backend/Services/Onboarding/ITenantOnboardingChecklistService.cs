using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Onboarding;

public sealed class OnboardingStepDto
{
    public string Step { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public sealed class TenantOnboardingOverviewDto
{
    public Guid TenantId { get; set; }
    public int CompletedCount { get; set; }
    public int TotalCount { get; set; }
    public bool IsFullyComplete { get; set; }
    public List<OnboardingStepDto> Steps { get; set; } = [];
}

public interface ITenantOnboardingChecklistService
{
    Task<TenantOnboardingOverviewDto> EnsureAndGetAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<TenantOnboardingOverviewDto> CompleteStepAsync(
        Guid tenantId,
        string step,
        string? actorUserId,
        CancellationToken cancellationToken = default);
}
