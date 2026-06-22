namespace KasseAPI_Final.Services.AdminTenants;

/// <summary>Sliding-window rate limit for mandant license PUT / extend attempts.</summary>
public interface ITenantLicenseExtensionRateLimiter
{
    /// <summary>Returns an error message when the attempt must be rejected; otherwise null.</summary>
    string? TryAcquireOrError(string? actorUserId, Guid tenantId);
}
