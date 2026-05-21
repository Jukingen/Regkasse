namespace KasseAPI_Final.Services.AdminTenants;

public static class TenantOnboardingErrorCodes
{
    public const string SlugTaken = "tenant_slug_taken";
    public const string SlugInvalid = "tenant_slug_invalid";
    public const string AdminEmailTaken = "tenant_admin_email_taken";
    public const string ProvisioningFailed = "tenant_provisioning_failed";
    public const string Unknown = "tenant_onboarding_failed";
}

public sealed record TenantOnboardingFailureDto(
    string Message,
    string Code,
    IReadOnlyList<string>? SlugSuggestions = null);

public sealed record TenantSlugSuggestionsDto(IReadOnlyList<string> Suggestions);
