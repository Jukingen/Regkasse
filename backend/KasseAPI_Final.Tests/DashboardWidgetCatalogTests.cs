using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.Dashboard;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>Catalog permission filter regressions (manage→view implications).</summary>
public sealed class DashboardWidgetCatalogTests
{
    [Fact]
    public void FilterByPermissions_CashRegisterManageImpliesView_IncludesActiveCashRegisters()
    {
        // Manager matrix grants manage, not view; catalog widgets require view.
        var filtered = DashboardWidgetCatalog.FilterByPermissions([AppPermissions.CashRegisterManage]);

        Assert.Contains(filtered, w => w.WidgetId == DashboardWidgetCatalog.ActiveCashRegisters);
        Assert.Contains(filtered, w => w.WidgetId == DashboardWidgetCatalog.ManagerTseHealth);
        Assert.Contains(filtered, w => w.WidgetId == DashboardWidgetCatalog.ManagerMonatsbeleg);
    }

    [Fact]
    public void FilterByPermissions_CashRegisterViewOnly_IncludesActiveCashRegisters()
    {
        var filtered = DashboardWidgetCatalog.FilterByPermissions([AppPermissions.CashRegisterView]);

        Assert.Contains(filtered, w => w.WidgetId == DashboardWidgetCatalog.ActiveCashRegisters);
        Assert.DoesNotContain(filtered, w => w.WidgetId == DashboardWidgetCatalog.TodaySales);
    }

    [Fact]
    public void FilterByPermissions_WithoutCashRegisterGrant_ExcludesActiveCashRegisters()
    {
        var filtered = DashboardWidgetCatalog.FilterByPermissions([AppPermissions.ReportView]);

        Assert.DoesNotContain(filtered, w => w.WidgetId == DashboardWidgetCatalog.ActiveCashRegisters);
        Assert.Contains(filtered, w => w.WidgetId == DashboardWidgetCatalog.TodaySales);
    }

    [Fact]
    public void BuildDefaultLayout_ManagerRole_IncludesActiveCashRegisters()
    {
        var permissions = RolePermissionMatrix.GetPermissionsForRoles([Roles.Manager]);
        var layout = DashboardWidgetCatalog.BuildDefaultLayout(permissions);

        Assert.Contains(layout, w => w.WidgetId == DashboardWidgetCatalog.ActiveCashRegisters && w.IsVisible);
    }

    [Fact]
    public void BuildDefaultLayout_ManagerRole_IncludesActionRequiredAndHidesSlimLicenseExpiry()
    {
        var permissions = RolePermissionMatrix.GetPermissionsForRoles([Roles.Manager]);
        var layout = DashboardWidgetCatalog.BuildDefaultLayout(permissions);

        Assert.Contains(layout, w => w.WidgetId == DashboardWidgetCatalog.ActionRequired && w.IsVisible);
        Assert.Contains(layout, w => w.WidgetId == DashboardWidgetCatalog.DailyClosing && w.IsVisible);
        Assert.Contains(layout, w => w.WidgetId == DashboardWidgetCatalog.ManagerLicenseStatus && w.IsVisible);
        Assert.Contains(layout, w => w.WidgetId == DashboardWidgetCatalog.LicenseExpiry && !w.IsVisible);
        var actionOrder = layout.Single(w => w.WidgetId == DashboardWidgetCatalog.ActionRequired).Order;
        var closingOrder = layout.Single(w => w.WidgetId == DashboardWidgetCatalog.DailyClosing).Order;
        Assert.True(closingOrder > actionOrder);
    }

    [Fact]
    public void FilterByPermissions_WithoutDailyClosingView_ExcludesDailyClosingWidget()
    {
        var filtered = DashboardWidgetCatalog.FilterByPermissions([AppPermissions.ReportView]);
        Assert.DoesNotContain(filtered, w => w.WidgetId == DashboardWidgetCatalog.DailyClosing);
    }
}
