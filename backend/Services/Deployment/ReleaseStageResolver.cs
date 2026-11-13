using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.Services.Deployment;

/// <summary>
/// Resolves <c>RELEASE_STAGE</c> (<c>dev</c>|<c>staging</c>|<c>canary</c>|<c>production</c>)
/// from config / env, host environment, and optional canary tenant lists.
/// </summary>
public static class ReleaseStageResolver
{
    public const string Dev = "dev";
    public const string Staging = "staging";
    public const string Canary = "canary";
    public const string Production = "production";

    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var v = raw.Trim().ToLowerInvariant();
        return v switch
        {
            "dev" or "development" => Dev,
            "staging" or "stage" => Staging,
            "canary" => Canary,
            "production" or "prod" => Production,
            _ => v,
        };
    }

    /// <summary>
    /// Prefer explicit <c>RELEASE_STAGE</c> / <c>Deployment:ReleaseStage</c>, then canary tenant overlay,
    /// then derive from <see cref="IHostEnvironment"/>.
    /// </summary>
    public static string Resolve(
        IHostEnvironment hostEnvironment,
        DeploymentOptions? options = null,
        Guid? tenantId = null,
        string? tenantSlug = null)
    {
        var fromEnvVar = Normalize(Environment.GetEnvironmentVariable("RELEASE_STAGE"));
        var fromOptions = Normalize(options?.ReleaseStage);
        var configured = !string.IsNullOrEmpty(fromEnvVar) ? fromEnvVar : fromOptions;

        if (configured == Canary)
            return Canary;

        if (IsCanaryTenant(options, tenantId, tenantSlug))
        {
            // Canary tenants on a Production (or explicit production) deploy show CANARY banner.
            if (string.IsNullOrEmpty(configured)
                || configured == Production
                || hostEnvironment.IsProduction())
            {
                return Canary;
            }
        }

        if (!string.IsNullOrEmpty(configured)
            && (configured == Dev || configured == Staging || configured == Production || configured == Canary))
        {
            return configured;
        }

        if (hostEnvironment.IsDevelopment())
            return Dev;
        if (hostEnvironment.IsStaging())
            return Staging;
        return Production;
    }

    public static bool IsCanaryTenant(
        DeploymentOptions? options,
        Guid? tenantId,
        string? tenantSlug)
    {
        if (options == null)
            return false;

        if (tenantId.HasValue
            && options.CanaryTenantIds is { Length: > 0 }
            && options.CanaryTenantIds.Any(id => id == tenantId.Value))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(tenantSlug)
            && options.CanaryTenantSlugs is { Length: > 0 })
        {
            return options.CanaryTenantSlugs.Any(s =>
                !string.IsNullOrWhiteSpace(s)
                && string.Equals(s.Trim(), tenantSlug.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }
}
