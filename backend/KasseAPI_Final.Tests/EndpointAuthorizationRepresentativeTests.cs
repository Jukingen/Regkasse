using System.Security.Claims;
using KasseAPI_Final.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Representative permission checks for key areas: Users, Catalog, Inventory, Reports, Settings, POS (Cart/Payment), TSE.
/// Uses AddAppAuthorization and PermissionAuthorizationHandler; authorization is permission/canonical-role based.
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

    private static ClaimsPrincipal UserWithPermissions(params string[] permissions)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "user-1"));
        foreach (var permission in permissions)
            identity.AddClaim(new Claim(PermissionCatalog.PermissionClaimType, permission));
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
    public async Task Inventory_InventoryDelete_Waiter_Denied()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Waiter), null, Policy(AppPermissions.InventoryDelete));
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

    [Fact]
    public async Task CashRegister_CashRegisterView_ManagerManageClaim_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(
            UserWithPermissions(AppPermissions.CashRegisterManage),
            null,
            Policy(AppPermissions.CashRegisterView));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task CashRegister_CashRegisterManage_Manager_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Manager), null, Policy(AppPermissions.CashRegisterManage));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task CashRegister_CashRegisterManage_SuperAdmin_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.SuperAdmin), null, Policy(AppPermissions.CashRegisterManage));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task CashRegister_CashRegisterManage_Cashier_Denied()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Cashier), null, Policy(AppPermissions.CashRegisterManage));
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CashRegister_CashRegisterDecommission_Manager_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Manager), null, Policy(AppPermissions.CashRegisterDecommission));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task CashRegister_CashRegisterDecommission_Cashier_Denied()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Cashier), null, Policy(AppPermissions.CashRegisterDecommission));
        Assert.False(result.Succeeded);
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
    public async Task Settings_SettingsManage_SuperAdmin_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.SuperAdmin), null, Policy(AppPermissions.SettingsManage));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Settings_SettingsManage_Manager_Denied()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Manager), null, Policy(AppPermissions.SettingsManage));
        Assert.False(result.Succeeded);
    }

    // --- Backup (tenant-scoped backup.manage; narrower than settings.manage) ---
    [Fact]
    public async Task Backup_BackupManage_Manager_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Manager), null, Policy(AppPermissions.BackupManage));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Backup_BackupManage_SuperAdmin_Allowed_ViaSettingsManageImplication()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.SuperAdmin), null, Policy(AppPermissions.BackupManage));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Backup_BackupManage_Cashier_Denied()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Cashier), null, Policy(AppPermissions.BackupManage));
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Backup_BackupManageClaimHolder_DoesNotSatisfy_SettingsManage()
    {
        // Escalation guard: holding only backup.manage must NOT satisfy the broad settings.manage policy.
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(
            UserWithPermissions(AppPermissions.BackupManage),
            null,
            Policy(AppPermissions.SettingsManage));
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Backup_SettingsManageClaimHolder_Satisfies_BackupManage()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(
            UserWithPermissions(AppPermissions.SettingsManage),
            null,
            Policy(AppPermissions.BackupManage));
        Assert.True(result.Succeeded);
    }

    // --- POS: Payment oversight vs floor take ---
    [Fact]
    public async Task POS_PaymentView_Manager_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Manager), null, Policy(AppPermissions.PaymentView));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task POS_SaleView_Manager_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Manager), null, Policy(AppPermissions.SaleView));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task POS_PaymentTake_Manager_Denied()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Manager), null, Policy(AppPermissions.PaymentTake));
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task POS_PaymentTake_Cashier_Allowed()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Cashier), null, Policy(AppPermissions.PaymentTake));
        Assert.True(result.Succeeded);
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
    public async Task SystemCritical_Manager_Denied()
    {
        var auth = BuildServices().GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(UserWithRole(Roles.Manager), null, Policy(AppPermissions.SystemCritical));
        Assert.False(result.Succeeded);
    }
}
