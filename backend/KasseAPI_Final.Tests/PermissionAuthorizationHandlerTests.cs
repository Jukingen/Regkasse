using System.Security.Claims;
using KasseAPI_Final.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Permission policy evaluation: PermissionAuthorizationHandler with role fallback and explicit permission claims.
/// Covers negative scenarios: Waiter no refund, Cashier no settings, Manager no audit cleanup, ReportViewer no payment.
/// </summary>
public class PermissionAuthorizationHandlerTests
{
    private static IServiceProvider BuildAuthorizationServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppAuthorization();
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal CreatePrincipalWithRoles(params string[] roles)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "user-id"));
        foreach (var role in roles)
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreatePrincipalWithPermissions(params string[] permissions)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "user-id"));
        foreach (var p in permissions)
            identity.AddClaim(new Claim(PermissionCatalog.PermissionClaimType, p));
        return new ClaimsPrincipal(identity);
    }

    private static string PermissionPolicy(string permission) => PermissionCatalog.PolicyPrefix + permission;

    // --- Negative scenarios (must deny) ---

    [Fact]
    public async Task PermissionPolicy_RefundCreate_Denies_Waiter_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles("Waiter");

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.RefundCreate));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task PermissionPolicy_SettingsManage_Denies_Cashier_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles("Cashier");

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.SettingsManage));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task PermissionPolicy_AuditCleanup_Denies_Manager_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles("Manager");

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.AuditCleanup));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task PermissionPolicy_PaymentTake_Denies_ReportViewer_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles("ReportViewer");

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.PaymentTake));

        Assert.False(result.Succeeded);
    }

    // --- Positive: role has permission ---

    [Fact]
    public async Task PermissionPolicy_RefundCreate_Allows_Cashier_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles("Cashier");

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.RefundCreate));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task PermissionPolicy_SettingsManage_Allows_SuperAdmin_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.SuperAdmin);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.SettingsManage));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task PermissionPolicy_PaymentTake_Allows_Cashier_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles("Cashier");

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.PaymentTake));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task PermissionPolicy_AuditCleanup_Allows_SuperAdmin_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.SuperAdmin);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.AuditCleanup));

        Assert.True(result.Succeeded);
    }

    // --- Permission claims take precedence over role ---

    [Fact]
    public async Task PermissionPolicy_When_ExplicitPermissionClaim_Permit_Even_If_Role_Would_Not_Have_It()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "user-id"));
        identity.AddClaim(new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.PaymentTake));
        identity.AddClaim(new Claim(ClaimTypes.Role, "ReportViewer"));
        var user = new ClaimsPrincipal(identity);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.PaymentTake));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task PermissionPolicy_NoRoles_NoPermissionClaims_Denies()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = new ClaimsPrincipal(new ClaimsIdentity("Test"));

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.PaymentTake));

        Assert.False(result.Succeeded);
    }

    // --- UserView / UserManage ---

    [Fact]
    public async Task PermissionPolicy_UserView_Allows_Manager_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles("Manager");

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.UserView));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task PermissionPolicy_UserManage_Denies_Manager_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles("Manager");

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.UserManage));

        Assert.False(result.Succeeded);
    }

    // --- SystemCritical: SuperAdmin only ---

    [Fact]
    public async Task PermissionPolicy_SystemCritical_Allows_SuperAdmin_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.SuperAdmin);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.SystemCritical));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task PermissionPolicy_SystemCritical_Denies_Manager_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.Manager);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.SystemCritical));

        Assert.False(result.Succeeded);
    }

    // --- InventoryDelete: SuperAdmin only ---

    [Fact]
    public async Task PermissionPolicy_InventoryDelete_Allows_SuperAdmin_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.SuperAdmin);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.InventoryDelete));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task PermissionPolicy_InventoryDelete_Denies_Manager_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.Manager);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.InventoryDelete));

        Assert.False(result.Succeeded);
    }

    // --- TseDiagnostics: SuperAdmin only ---

    [Fact]
    public async Task PermissionPolicy_TseDiagnostics_Allows_SuperAdmin_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.SuperAdmin);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.TseDiagnostics));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task PermissionPolicy_TseDiagnostics_Denies_Manager_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.Manager);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.TseDiagnostics));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task PermissionPolicy_TseDiagnostics_Denies_Cashier_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.Cashier);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.TseDiagnostics));

        Assert.False(result.Succeeded);
    }

    // --- CartManage: Waiter denied ---

    [Fact]
    public async Task PermissionPolicy_CartManage_Denies_Waiter_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.Waiter);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.CartManage));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task PermissionPolicy_CartManage_Allows_Cashier_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.Cashier);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.CartManage));

        Assert.True(result.Succeeded);
    }
}
