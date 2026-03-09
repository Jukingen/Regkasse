using System.Security.Claims;
using KasseAPI_Final.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Permission-first: verifies user.view and user.manage via AddAppAuthorization (PermissionAuthorizationHandler).
/// Admin and SuperAdmin have both; Manager has user.view only; Cashier and Waiter have neither.
/// </summary>
public class UserManagementAuthorizationPolicyTests
{
    private static IServiceProvider BuildAuthorizationServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppAuthorization();
        return services.BuildServiceProvider();
    }

    private static string PermissionPolicy(string permission) => PermissionCatalog.PolicyPrefix + permission;

    private static ClaimsPrincipal CreatePrincipalWithRoles(params string[] roles)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "user-id"));
        foreach (var role in roles)
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(identity);
    }

    // --- user.view: Admin, SuperAdmin, Manager allowed; Cashier, Waiter denied ---

    [Fact]
    public async Task UserView_Permission_Allows_Admin_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.Admin);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.UserView));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task UserView_Permission_Allows_SuperAdmin_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.SuperAdmin);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.UserView));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task UserView_Permission_Allows_Manager_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.Manager);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.UserView));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task UserView_Permission_Denies_Cashier_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.Cashier);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.UserView));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task UserView_Permission_Denies_Waiter_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.Waiter);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.UserView));

        Assert.False(result.Succeeded);
    }

    // --- user.manage: Admin, SuperAdmin allowed; Manager, Cashier, Waiter denied ---

    [Fact]
    public async Task UserManage_Permission_Allows_Admin_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.Admin);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.UserManage));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task UserManage_Permission_Allows_SuperAdmin_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.SuperAdmin);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.UserManage));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task UserManage_Permission_Denies_Manager_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.Manager);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.UserManage));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task UserManage_Permission_Denies_Cashier_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = CreatePrincipalWithRoles(Roles.Cashier);

        var result = await auth.AuthorizeAsync(user, null, PermissionPolicy(AppPermissions.UserManage));

        Assert.False(result.Succeeded);
    }
}
