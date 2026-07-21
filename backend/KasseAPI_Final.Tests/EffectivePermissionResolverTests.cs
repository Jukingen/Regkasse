using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class EffectivePermissionResolverTests
{
    [Fact]
    public async Task GrantOverride_AddsPermissionNotInRole()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid().ToString("D");
        db.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            UserId = userId,
            Permission = AppPermissions.ReportExport,
            IsGranted = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var resolver = new EffectivePermissionResolver(db, new TestRolePermissionResolver(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppPermissions.ReportView,
        }));

        var effective = await resolver.GetEffectivePermissionsAsync(userId, new[] { "ReportViewer" });

        Assert.Contains(AppPermissions.ReportView, effective);
        Assert.Contains(AppPermissions.ReportExport, effective);
    }

    [Fact]
    public async Task DenyOverride_RemovesRolePermission()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid().ToString("D");
        db.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            UserId = userId,
            Permission = AppPermissions.PaymentCancel,
            IsGranted = false,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var roleResolver = new TestRolePermissionResolver(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppPermissions.PaymentView,
            AppPermissions.PaymentCancel,
        });
        var resolver = new EffectivePermissionResolver(db, roleResolver);

        var effective = await resolver.GetEffectivePermissionsAsync(userId, new[] { "Cashier" });

        Assert.Contains(AppPermissions.PaymentView, effective);
        Assert.DoesNotContain(AppPermissions.PaymentCancel, effective);
    }

    [Fact]
    public async Task TenantScopedOverride_AppliesOnlyForMatchingTenant()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid().ToString("D");
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        db.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            UserId = userId,
            TenantId = tenantA,
            Permission = AppPermissions.UserCreate,
            IsGranted = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var roleResolver = new TestRolePermissionResolver(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppPermissions.UserView,
        });
        var resolver = new EffectivePermissionResolver(db, roleResolver);

        var forTenantA = await resolver.GetEffectivePermissionsAsync(userId, new[] { "Manager" }, tenantA);
        var forTenantB = await resolver.GetEffectivePermissionsAsync(userId, new[] { "Manager" }, tenantB);

        Assert.Contains(AppPermissions.UserCreate, forTenantA);
        Assert.DoesNotContain(AppPermissions.UserCreate, forTenantB);
    }

    private static AppDbContext CreateDb()
    {
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"EffPerm_{Guid.NewGuid():N}")
                .Options,
            NullCurrentTenantAccessor.Instance);
    }

    private sealed class TestRolePermissionResolver : IRolePermissionResolver
    {
        private readonly IReadOnlySet<string> _permissions;

        public TestRolePermissionResolver(IReadOnlySet<string> permissions) => _permissions = permissions;

        public Task<IReadOnlySet<string>> GetPermissionsForRolesAsync(
            IEnumerable<string> roleNames,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_permissions);
    }
}

public sealed class PermissionImplicationTests
{
    [Fact]
    public void UserManage_Satisfies_UserView_ResetPassword_And_UserDelete()
    {
        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AppPermissions.UserManage };
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.UserView, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.UserResetPassword, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.UserCreate, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.UserDelete, effective));
    }

    [Fact]
    public void ReportExport_Satisfies_ReportView()
    {
        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AppPermissions.ReportExport };
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.ReportView, effective));
        Assert.False(PermissionImplication.IsSatisfied(AppPermissions.ReportExport,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AppPermissions.ReportView }));
    }

    [Fact]
    public void UserCreate_DoesNotSatisfy_UserDelete_WithoutParentOrChild()
    {
        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AppPermissions.UserCreate };
        Assert.False(PermissionImplication.IsSatisfied(AppPermissions.UserDelete, effective));
    }

    [Fact]
    public void CashRegisterManage_Satisfies_CashRegisterView()
    {
        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppPermissions.CashRegisterManage,
        };
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.CashRegisterView, effective));
    }

    [Fact]
    public void CashRegisterView_DoesNotSatisfy_CashRegisterManage()
    {
        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppPermissions.CashRegisterView,
        };
        Assert.False(PermissionImplication.IsSatisfied(AppPermissions.CashRegisterManage, effective));
    }

    [Fact]
    public void AllGranularChildren_Satisfy_UserManage()
    {
        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppPermissions.UserCreate,
            AppPermissions.UserEdit,
            AppPermissions.UserDelete,
            AppPermissions.UserChangeRole,
            AppPermissions.UserChangeUsername,
            AppPermissions.UserResetPassword,
        };
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.UserManage, effective));
    }

    [Fact]
    public void DigitalManage_Satisfies_Simplified_And_Legacy()
    {
        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppPermissions.DigitalManage,
        };
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalView, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalPreview, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalRequest, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalCreate, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalPublish, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalEdit, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalDelete, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalWebCreate, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalAppCreate, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalPricingManage, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalActivate, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalOrdersView, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalOrdersManage, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalOrdersApprove, effective));
    }

    [Fact]
    public void DigitalOrdersManage_Satisfies_View_Not_Approve()
    {
        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppPermissions.DigitalOrdersManage,
        };
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalOrdersView, effective));
        Assert.False(PermissionImplication.IsSatisfied(AppPermissions.DigitalOrdersApprove, effective));
    }

    [Fact]
    public void DigitalView_Satisfies_Legacy_WebAndApp_View()
    {
        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppPermissions.DigitalView,
        };
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalWebView, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalAppView, effective));
        Assert.False(PermissionImplication.IsSatisfied(AppPermissions.DigitalCreate, effective));
    }

    [Fact]
    public void WebsiteManage_Satisfies_Digital_ViewPreviewRequest_NotCreate()
    {
        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppPermissions.WebsiteManage,
        };
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalView, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalPreview, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalRequest, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalWebView, effective));
        Assert.False(PermissionImplication.IsSatisfied(AppPermissions.DigitalCreate, effective));
        Assert.False(PermissionImplication.IsSatisfied(AppPermissions.DigitalWebUse, effective));
        Assert.False(PermissionImplication.IsSatisfied(AppPermissions.DigitalManage, effective));
    }

    [Fact]
    public void DigitalCreate_Satisfies_Legacy_CreateAndUse()
    {
        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppPermissions.DigitalCreate,
        };
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalView, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalWebCreate, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalAppCreate, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalWebUse, effective));
        Assert.False(PermissionImplication.IsSatisfied(AppPermissions.DigitalPublish, effective));
    }

    [Fact]
    public void DigitalWebCreate_Satisfies_DigitalWebViewPreviewAndUse()
    {
        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppPermissions.DigitalWebCreate,
        };
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalWebView, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalWebPreview, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalWebUse, effective));
        Assert.False(PermissionImplication.IsSatisfied(AppPermissions.DigitalAppCreate, effective));
    }

    [Fact]
    public void DigitalWebUse_Satisfies_DigitalWebView()
    {
        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppPermissions.DigitalWebUse,
        };
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.DigitalWebView, effective));
        Assert.False(PermissionImplication.IsSatisfied(AppPermissions.DigitalAppUse, effective));
    }
}
