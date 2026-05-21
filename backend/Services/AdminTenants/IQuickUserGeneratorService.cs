namespace KasseAPI_Final.Services.AdminTenants;

/// <summary>Prepares quick-tenant-user credentials (email, password) with rate limiting and uniqueness checks.</summary>
public interface IQuickUserGeneratorService
{
    /// <summary>Validates rate limit, role, and allocates a unique email plus secure password.</summary>
    Task<(QuickUserGenerationPlan? Plan, string? Error)> PrepareAsync(
        Guid tenantId,
        string role,
        CancellationToken cancellationToken = default);
}

public sealed record QuickUserGenerationPlan(
    string Email,
    string Password,
    string Role,
    string TenantSlug);
