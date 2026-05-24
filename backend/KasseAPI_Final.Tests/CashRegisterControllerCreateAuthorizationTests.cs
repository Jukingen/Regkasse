using System.Security.Claims;
using KasseAPI_Final.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// POST api/CashRegister is protected by [HasPermission(CashRegisterManage)].
/// Failed policy evaluation yields HTTP 403 from the authorization middleware.
/// </summary>
public class CashRegisterControllerCreateAuthorizationTests
{
    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppAuthorization();
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal UserWithRole(string role)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "user-1"));
        identity.AddClaim(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(identity);
    }

    private static string Policy(string permission) => PermissionCatalog.PolicyPrefix + permission;

    [Fact]
    public void CreateCashRegister_Requires_CashRegisterManage_SuperAdmin_Has_Permission()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.CashRegisterManage));
    }

    [Fact]
    public void CreateCashRegister_Requires_CashRegisterManage_Manager_Has_Permission()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.CashRegisterManage));
    }

    [Fact]
    public void CreateCashRegister_Requires_CashRegisterManage_Cashier_DoesNotHave_Permission()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.CashRegisterManage));
    }

    [Fact]
    public void CreateCashRegister_Requires_CashRegisterManage_Waiter_DoesNotHave_Permission()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Waiter, AppPermissions.CashRegisterManage));
    }

    /// <summary>Cashier lacks cash_register.manage — ASP.NET returns 403 on POST /api/CashRegister.</summary>
    [Fact]
    public async Task CreateCashRegister_Cashier_Returns403()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(
            UserWithRole(Roles.Cashier),
            null,
            Policy(AppPermissions.CashRegisterManage));

        Assert.False(result.Succeeded);
    }
}
