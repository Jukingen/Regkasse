namespace KasseAPI_Final.Services.AdminTenants;

public interface ITenantOnboardingService
{
    Task<IReadOnlyList<string>> GetSlugSuggestionsAsync(
        string? companyName,
        string? preferredSlug,
        int maxCount = 5,
        CancellationToken cancellationToken = default);

    Task<(AdminTenantDetailDto? Result, TenantOnboardingFailureDto? Failure)> CreateAsync(
        CreateAdminTenantRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default);
}
