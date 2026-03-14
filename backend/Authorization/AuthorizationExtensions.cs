using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using KasseAPI_Final.Middleware;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Extension methods for permission-based and app-context authorization registration.
/// Registers one policy per permission in PermissionCatalog.All (policy name: "Permission:{permission}")
/// and per-app context policies ("AppContext:pos", "AppContext:admin").
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Registers the full app authorization layer: permission-based policies, app-context policies,
    /// PermissionAuthorizationHandler, AppContextAuthorizationHandler, and ForbiddenResponseAuthorizationHandler.
    /// </summary>
    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPermissionPolicies();
            options.AddAppContextPolicies();
        });
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, AppContextAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ForbiddenResponseAuthorizationHandler>();
        return services;
    }

    /// <summary>
    /// Registers a policy for each permission in PermissionCatalog.All.
    /// Policy name: PermissionCatalog.PolicyPrefix + permission (e.g. "Permission:payment.take").
    /// </summary>
    public static AuthorizationOptions AddPermissionPolicies(this AuthorizationOptions options)
    {
        foreach (var permission in PermissionCatalog.All)
        {
            var policyName = PermissionCatalog.PolicyPrefix + permission;
            options.AddPolicy(policyName, policy =>
                policy.Requirements.Add(new PermissionRequirement(permission)));
        }
        return options;
    }

    /// <summary>
    /// Registers app-context policies for known client apps (pos, admin).
    /// Policy name: "AppContext:{app}" (e.g. "AppContext:pos").
    /// Enforcement is flag-gated via <see cref="AppContextAuthorizationHandler"/>.
    /// </summary>
    private static void AddAppContextPolicies(this AuthorizationOptions options)
    {
        foreach (var app in ClientAppPolicy.KnownApps)
        {
            var policyName = RequireAppContextAttribute.PolicyPrefix + app;
            options.AddPolicy(policyName, policy =>
                policy.Requirements.Add(new AppContextRequirement(app)));
        }
    }
}
