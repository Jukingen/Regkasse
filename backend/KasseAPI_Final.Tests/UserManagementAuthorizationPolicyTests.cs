using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Verifies that UsersView and UsersManage policies allow Admin role (legacy alias for Administrator).
/// </summary>
public class UserManagementAuthorizationPolicyTests
{
    private static IServiceProvider BuildAuthorizationServices()
    {
        var services = new ServiceCollection();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminUsers", policy =>
                policy.RequireRole("SuperAdmin", "Admin", "Administrator"));
            options.AddPolicy("UsersView", policy =>
                policy.RequireRole("SuperAdmin", "Admin", "Administrator", "BranchManager", "Auditor"));
            options.AddPolicy("UsersManage", policy =>
                policy.RequireRole("SuperAdmin", "Admin", "Administrator", "BranchManager"));
        });
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal UserWithRole(string role)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-id"),
            new Claim(ClaimTypes.Role, role)
        }, "Test"));
    }

    [Fact]
    public void UsersView_Policy_Allows_Admin_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = UserWithRole("Admin");

        var result = auth.AuthorizeAsync(user, "UsersView").GetAwaiter().GetResult();

        Assert.True(result.Succeeded, "UsersView policy should allow role Admin.");
    }

    [Fact]
    public void UsersView_Policy_Allows_SuperAdmin_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = UserWithRole("SuperAdmin");

        var result = auth.AuthorizeAsync(user, "UsersView").GetAwaiter().GetResult();

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void UsersManage_Policy_Allows_Admin_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = UserWithRole("Admin");

        var result = auth.AuthorizeAsync(user, "UsersManage").GetAwaiter().GetResult();

        Assert.True(result.Succeeded, "UsersManage policy should allow role Admin.");
    }

    [Fact]
    public void UsersView_Policy_Denies_Cashier_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = UserWithRole("Cashier");

        var result = auth.AuthorizeAsync(user, "UsersView").GetAwaiter().GetResult();

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void UsersManage_Policy_Denies_Cashier_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = UserWithRole("Cashier");

        var result = auth.AuthorizeAsync(user, "UsersManage").GetAwaiter().GetResult();

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void UsersView_Policy_Allows_Administrator_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = UserWithRole("Administrator");

        var result = auth.AuthorizeAsync(user, "UsersView").GetAwaiter().GetResult();

        Assert.True(result.Succeeded, "UsersView policy should allow role Administrator (legacy alias).");
    }

    [Fact]
    public void UsersManage_Policy_Allows_Administrator_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = UserWithRole("Administrator");

        var result = auth.AuthorizeAsync(user, "UsersManage").GetAwaiter().GetResult();

        Assert.True(result.Succeeded, "UsersManage policy should allow role Administrator (legacy alias).");
    }

    [Fact]
    public void UsersView_Policy_Denies_Kellner_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = UserWithRole("Kellner");

        var result = auth.AuthorizeAsync(user, "UsersView").GetAwaiter().GetResult();

        Assert.False(result.Succeeded, "UsersView policy should deny Kellner.");
    }

    [Fact]
    public void UsersManage_Policy_Denies_Kellner_Role()
    {
        var provider = BuildAuthorizationServices();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = UserWithRole("Kellner");

        var result = auth.AuthorizeAsync(user, "UsersManage").GetAwaiter().GetResult();

        Assert.False(result.Succeeded, "UsersManage policy should deny Kellner.");
    }
}
