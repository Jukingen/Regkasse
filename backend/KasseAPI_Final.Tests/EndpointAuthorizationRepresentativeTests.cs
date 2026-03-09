using System.Security.Claims;
using KasseAPI_Final.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Representative permission checks for key areas: Users, Catalog, Inventory, Reports, Settings, POS (Cart/Payment), TSE.
/// Uses AddAppAuthorization and PermissionAuthorizationHandler; no legacy role policies.
/// </summary>
public class EndpointAuthorizationRepresentativeTests
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

    // --- Users ---
    [Fact]
    public async Task Users_UserView_Manager_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Manager), null, Policy(AppPermissions.UserView));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Users_UserManage_Cashier_Denied()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Cashier), null, Policy(AppPermissions.UserManage));
        Assert.False(result.Succeeded);
    }

    // --- Catalog ---
    [Fact]
    public async Task Catalog_ProductManage_Manager_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Manager), null, Policy(AppPermissions.ProductManage));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Catalog_ProductView_Cashier_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Cashier), null, Policy(AppPermissions.ProductView));
        Assert.True(result.Succeeded);
    }

    // --- Inventory ---
    [Fact]
    public async Task Inventory_InventoryView_Cashier_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Cashier), null, Policy(AppPermissions.InventoryView));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Inventory_InventoryDelete_Manager_Denied()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Manager), null, Policy(AppPermissions.InventoryDelete));
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Inventory_InventoryDelete_Admin_Denied()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Admin), null, Policy(AppPermissions.InventoryDelete));
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Inventory_InventoryDelete_SuperAdmin_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.SuperAdmin), null, Policy(AppPermissions.InventoryDelete));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Settings_SettingsView_Cashier_Denied()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Cashier), null, Policy(AppPermissions.SettingsView));
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Settings_SettingsView_Manager_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Manager), null, Policy(AppPermissions.SettingsView));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Reports_ReportExport_ReportViewer_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.ReportViewer), null, Policy(AppPermissions.ReportExport));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Reports_ReportExport_Cashier_Denied()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Cashier), null, Policy(AppPermissions.ReportExport));
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CashRegister_CashRegisterView_Cashier_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Cashier), null, Policy(AppPermissions.CashRegisterView));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task CashRegister_CashdrawerOpen_Cashier_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Cashier), null, Policy(AppPermissions.CashdrawerOpen));
        Assert.True(result.Succeeded);
    }

    // --- Reports ---
    [Fact]
    public async Task Reports_ReportView_ReportViewer_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.ReportViewer), null, Policy(AppPermissions.ReportView));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Reports_ReportView_Cashier_Denied()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Cashier), null, Policy(AppPermissions.ReportView));
        Assert.False(result.Succeeded);
    }

    // --- Settings ---
    [Fact]
    public async Task Settings_SettingsManage_Admin_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Admin), null, Policy(AppPermissions.SettingsManage));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Settings_SettingsManage_Manager_Denied()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Manager), null, Policy(AppPermissions.SettingsManage));
        Assert.False(result.Succeeded);
    }

    // --- POS: CartManage ---
    [Fact]
    public async Task POS_CartManage_Cashier_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Cashier), null, Policy(AppPermissions.CartManage));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task POS_CartManage_Waiter_Denied()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Waiter), null, Policy(AppPermissions.CartManage));
        Assert.False(result.Succeeded);
    }

    // --- TSE ---
    [Fact]
    public async Task TSE_TseSign_Cashier_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Cashier), null, Policy(AppPermissions.TseSign));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task TSE_TseDiagnostics_Cashier_Denied()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Cashier), null, Policy(AppPermissions.TseDiagnostics));
        Assert.False(result.Succeeded);
    }

    // --- SystemCritical ---
    [Fact]
    public async Task SystemCritical_SuperAdmin_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.SuperAdmin), null, Policy(AppPermissions.SystemCritical));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task SystemCritical_Admin_Denied()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Admin), null, Policy(AppPermissions.SystemCritical));
        Assert.False(result.Succeeded);
    }
}
