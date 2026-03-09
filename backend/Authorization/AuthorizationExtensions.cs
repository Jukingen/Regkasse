using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using KasseAPI_Final.Middleware;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Extension methods for permission-based authorization registration.
/// Registers one policy per permission in PermissionCatalog.All (policy name: "Permission:{permission}").
/// All endpoint authorization is permission-first via [HasPermission(AppPermissions.X)]; legacy role policies are no longer registered.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Registers the full app authorization layer: permission-based policies (one per PermissionCatalog.All),
    /// PermissionAuthorizationHandler, and ForbiddenResponseAuthorizationHandler. Endpoints use [HasPermission(AppPermissions.X)].
    /// </summary>
    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            AddLegacyRolePolicies(options);
            options.AddPermissionPolicies();
        });
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ForbiddenResponseAuthorizationHandler>();
        return services;
    }

    /// <summary>
    /// Registers a policy for each permission in PermissionCatalog.All.
    /// Policy name: PermissionCatalog.PolicyPrefix + permission (e.g. "Permission:payment.take").
    /// Evaluated by PermissionAuthorizationHandler (JWT permission claims or RolePermissionMatrix).
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
    /// Legacy role-based policies: not registered. All endpoint protection is explicit via [HasPermission(AppPermissions.X)].
    /// No bridge or coarse role policies; critical endpoints (Settings, CashRegister, SystemCritical) use permission attributes only.
    /// </summary>
    private static void AddLegacyRolePolicies(AuthorizationOptions options)
    {
        // Intentionally empty: permission-first only. Do not add role-based policies here.
    }
}
