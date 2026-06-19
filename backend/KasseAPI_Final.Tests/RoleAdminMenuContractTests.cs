using KasseAPI_Final.Authorization;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Cross-layer contract: admin JWT permission sets per role must match FA menu matrix expectations.
/// Frontend mirror: frontend-admin/src/shared/__tests__/fixtures/adminAppPermissionFixtures.ts
/// </summary>
public class RoleAdminMenuContractTests
{
    private static readonly string[] CashierAdminAllowlist =
    {
        AppPermissions.ProductView,
        AppPermissions.CategoryView,
        AppPermissions.ModifierView,
        AppPermissions.PaymentView,
        AppPermissions.ReportView,
    };

    [Fact]
    public void AdminProfile_Cashier_MatchesFaAllowlistExactly()
    {
        var effective = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Cashier });
        var filtered = AdminAppPermissionProfile.Filter(
            ClientAppPolicy.Admin,
            new[] { Roles.Cashier },
            effective);

        Assert.Equal(CashierAdminAllowlist.Length, filtered.Count);
        foreach (var expected in CashierAdminAllowlist)
            Assert.Contains(expected, filtered);
    }

    [Theory]
    [InlineData(AppPermissions.TableView)]
    [InlineData(AppPermissions.SaleView)]
    [InlineData(AppPermissions.ShiftView)]
    [InlineData(AppPermissions.TseSign)]
    [InlineData(AppPermissions.CashRegisterView)]
    [InlineData(AppPermissions.UserView)]
    [InlineData(AppPermissions.SettingsView)]
    public void AdminProfile_Cashier_ExcludesAdminAndPosMenus(string permission)
    {
        var effective = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Cashier });
        var filtered = AdminAppPermissionProfile.Filter(
            ClientAppPolicy.Admin,
            new[] { Roles.Cashier },
            effective);

        Assert.DoesNotContain(permission, filtered);
    }

    [Theory]
    [InlineData(Roles.Manager, AppPermissions.UserView)]
    [InlineData(Roles.Manager, AppPermissions.CashRegisterManage)]
    [InlineData(Roles.Manager, AppPermissions.FinanzOnlineManage)]
    [InlineData(Roles.Manager, AppPermissions.PaymentView)]
    [InlineData(Roles.Manager, AppPermissions.SaleView)]
    [InlineData(Roles.Manager, AppPermissions.ReportView)]
    [InlineData(Roles.Manager, AppPermissions.ReportExport)]
    [InlineData(Roles.Accountant, AppPermissions.ReportExport)]
    [InlineData(Roles.ReportViewer, AppPermissions.AuditView)]
    public void AdminProfile_BackOfficeRoles_KeepAdminPermissions(string role, string permission)
    {
        var effective = RolePermissionMatrix.GetPermissionsForRoles(new[] { role });
        var filtered = AdminAppPermissionProfile.Filter(
            ClientAppPolicy.Admin,
            new[] { role },
            effective);

        Assert.Contains(permission, filtered);
    }

    [Fact]
    public void AdminProfile_Manager_KeepsAllOversightViewPermissionsForFaMenus()
    {
        var effective = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Manager });
        var filtered = AdminAppPermissionProfile.Filter(
            ClientAppPolicy.Admin,
            new[] { Roles.Manager },
            effective);

        foreach (var viewKey in AdminAppPermissionProfile.ManagerOversightViewPermissions)
        {
            Assert.Contains(viewKey, effective);
            Assert.Contains(viewKey, filtered);
        }

        Assert.DoesNotContain(AppPermissions.PaymentTake, filtered);
        Assert.DoesNotContain(AppPermissions.TseSign, filtered);
        Assert.DoesNotContain(AppPermissions.CartManage, filtered);
    }

    [Theory]
    [InlineData(AppPermissions.PaymentTake)]
    [InlineData(AppPermissions.TseSign)]
    [InlineData(AppPermissions.CartManage)]
    [InlineData(AppPermissions.SaleCreate)]
    public void AdminProfile_Manager_ExcludesPosTerminalWritePermissions(string permission)
    {
        var effective = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Manager });
        var filtered = AdminAppPermissionProfile.Filter(
            ClientAppPolicy.Admin,
            new[] { Roles.Manager },
            effective);

        Assert.DoesNotContain(permission, filtered);
    }
}
