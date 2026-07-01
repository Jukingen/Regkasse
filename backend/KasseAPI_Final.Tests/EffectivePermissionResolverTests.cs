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
    public void UserManage_Satisfies_UserCreate()
    {
        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AppPermissions.UserManage };
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.UserCreate, effective));
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
}
