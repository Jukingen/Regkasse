using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using KasseAPI_Final.Middleware;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Extension methods for permission-based authorization registration.
/// Registers one policy per permission in PermissionCatalog.All (policy name: "Permission:{permission}").
/// Legacy: Role-based policies remain; new endpoints use [HasPermission(AppPermissions.X)] or [Authorize(Policy = "Permission:...")].
/// Administrator role is mapped to Admin permission set in RolePermissionMatrix (legacy alias).
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Registers the full app authorization layer: legacy role-based policies, permission-based policies,
    /// PermissionAuthorizationHandler, and ForbiddenResponseAuthorizationHandler.
    /// Legacy compatibility: existing [Authorize(Policy = "PosSales")] etc. stay; new endpoints use [HasPermission].
    /// Administrator → Admin permission set in RolePermissionMatrix.
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
    /// Legacy role-based policies. Kept for backward compatibility; do not remove until all endpoints use permission policies.
    /// Uses Roles.* constants to avoid magic strings.
    /// </summary>
    private static void AddLegacyRolePolicies(AuthorizationOptions options)
    {
        options.AddPolicy("AdminUsers", policy =>
            policy.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Administrator));
        options.AddPolicy("UsersView", policy =>
            policy.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Administrator, "BranchManager", "Auditor"));
        options.AddPolicy("UsersManage", policy =>
            policy.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Administrator, "BranchManager"));

        options.AddPolicy("BackofficeManagement", policy =>
            policy.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Administrator, Roles.Manager));
        options.AddPolicy("BackofficeSettings", policy =>
            policy.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Administrator));

        options.AddPolicy("PosSales", policy =>
            policy.RequireRole(Roles.Cashier, Roles.Manager, Roles.Admin, Roles.Administrator, Roles.SuperAdmin));
        options.AddPolicy("PosTableOrder", policy =>
            policy.RequireRole(Roles.Waiter, Roles.Cashier, Roles.Manager, Roles.Admin, Roles.Administrator, Roles.SuperAdmin));
        options.AddPolicy("PosCatalogRead", policy =>
            policy.RequireRole(Roles.Waiter, Roles.Cashier, Roles.Manager, Roles.Admin, Roles.Administrator, Roles.SuperAdmin));

        options.AddPolicy("CatalogManage", policy =>
            policy.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Administrator, Roles.Manager));
        options.AddPolicy("InventoryManage", policy =>
            policy.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Administrator, Roles.Manager));
        options.AddPolicy("InventoryDelete", policy =>
            policy.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Administrator));

        options.AddPolicy("AuditView", policy =>
            policy.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Administrator, Roles.Manager));
        options.AddPolicy("AuditViewWithCashier", policy =>
            policy.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Administrator, Roles.Manager, Roles.Cashier));
        options.AddPolicy("AuditAdmin", policy =>
            policy.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Administrator));

        options.AddPolicy("PosTse", policy =>
            policy.RequireRole(Roles.Cashier, Roles.Manager, Roles.Admin, Roles.Administrator, Roles.SuperAdmin));
        options.AddPolicy("PosTseDiagnostics", policy =>
            policy.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Administrator));

        options.AddPolicy("SystemCritical", policy =>
            policy.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Administrator));
        options.AddPolicy("CashRegisterManage", policy =>
            policy.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Administrator));
    }
}
