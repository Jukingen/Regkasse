using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;
using System.Security.Claims;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalySettingsServiceTests
{
    [Fact]
    public void GetSettings_UsesConfigDefaultWhenNoOverride()
    {
        var (factory, cache) = CreateStore(new FiskalyOptions { Enabled = true, ApiKey = "k", ApiSecret = "s" });
        var svc = CreateService(factory, cache, new FiskalyOptions { Enabled = true, ApiKey = "k", ApiSecret = "s" });

        var settings = svc.GetSettings();

        Assert.True(settings.Enabled);
        Assert.True(settings.IsConfigured);
        Assert.Equal("config", settings.Source);
        Assert.Equal("TEST", settings.Environment);
    }

    [Fact]
    public async Task UpdateEnabled_PersistsGlobalOverride()
    {
        var opts = new FiskalyOptions { Enabled = true, ApiKey = "k", ApiSecret = "s" };
        var (factory, cache) = CreateStore(opts);
        var svc = CreateService(factory, cache, opts);

        var updated = await svc.UpdateEnabledAsync(false, "sa-1");

        Assert.False(updated.Enabled);
        Assert.Equal("global_override", updated.Source);
        Assert.False(cache.IsEnabled(true));

        await using var db = factory.CreateDbContext();
        var row = Assert.Single(db.TenantSettings.Where(s => s.Key == FiskalyEnabledOverrideCache.SettingsKey));
        Assert.Null(row.TenantId);
        Assert.Equal("false", row.Value);
    }

    [Fact]
    public async Task UpdateEnabled_WithAmbientTenant_PersistsTenantOverride()
    {
        var tenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var opts = new FiskalyOptions { Enabled = true, ApiKey = "k", ApiSecret = "s" };
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var (factory, cache) = CreateStore(opts, accessor);
        var svc = CreateService(factory, cache, opts, tenantAccessor: accessor);

        var updated = await svc.UpdateEnabledAsync(false, "manager-1");

        Assert.False(updated.Enabled);
        Assert.Equal("tenant_override", updated.Source);

        await using var db = factory.CreateDbContext();
        var row = Assert.Single(db.TenantSettings.Where(s => s.Key == FiskalyEnabledOverrideCache.SettingsKey));
        Assert.Equal(tenantId, row.TenantId);
        Assert.Equal("false", row.Value);
        Assert.False(cache.Resolve(tenantId).Overlay);
        Assert.Null(cache.Resolve(null).Overlay);
    }

    [Fact]
    public void GetSettings_TenantOverride_WinsOverGlobal()
    {
        var tenantId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        var opts = new FiskalyOptions { Enabled = true, ApiKey = "k", ApiSecret = "s" };
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var (factory, cache) = CreateStore(opts, accessor);

        using (var db = factory.CreateDbContext())
        {
            db.TenantSettings.Add(new TenantSetting
            {
                TenantId = null,
                Key = FiskalyEnabledOverrideCache.SettingsKey,
                Value = "false",
            });
            db.TenantSettings.Add(new TenantSetting
            {
                TenantId = tenantId,
                Key = FiskalyEnabledOverrideCache.SettingsKey,
                Value = "true",
            });
            db.SaveChanges();
        }

        var svc = CreateService(factory, cache, opts, tenantAccessor: accessor);
        var settings = svc.GetSettings();

        Assert.True(settings.Enabled);
        Assert.Equal("tenant_override", settings.Source);
    }

    [Fact]
    public async Task GetStatus_Disabled_DoesNotAuthenticate()
    {
        var client = new Mock<IFiskalyClient>(MockBehavior.Strict);
        var opts = new FiskalyOptions { Enabled = false, ApiKey = "k", ApiSecret = "s" };
        var (factory, cache) = CreateStore(opts);
        var svc = CreateService(factory, cache, opts, client.Object);

        var status = await svc.GetStatusAsync();

        Assert.False(status.IsEnabled);
        Assert.True(status.IsConfigured);
        Assert.False(status.IsAuthenticated);
        client.Verify(c => c.AuthenticateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (IDbContextFactory<AppDbContext> Factory, FiskalyEnabledOverrideCache Cache) CreateStore(
        FiskalyOptions _,
        ICurrentTenantAccessor? tenantAccessor = null)
    {
        var dbName = $"FiskalySettings_{Guid.NewGuid():N}";
        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Returns(() => CreateDb(dbName));
        factory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CancellationToken _) => CreateDb(dbName));
        var cache = new FiskalyEnabledOverrideCache(
            factory.Object,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<FiskalyEnabledOverrideCache>.Instance,
            tenantAccessor);
        return (factory.Object, cache);
    }

    private static AppDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(null));
    }

    private static FiskalySettingsService CreateService(
        IDbContextFactory<AppDbContext> factory,
        FiskalyEnabledOverrideCache cache,
        FiskalyOptions options,
        IFiskalyClient? client = null,
        ICurrentTenantAccessor? tenantAccessor = null)
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        return new FiskalySettingsService(
            Options.Create(options).ToMonitor(),
            cache,
            client ?? Mock.Of<IFiskalyClient>(),
            factory,
            audit.Object,
            NullLogger<FiskalySettingsService>.Instance,
            tenantAccessor);
    }
}

public sealed class AdminFiskalyControllerTests
{
    [Fact]
    public void GetSettings_ReturnsDto()
    {
        var settings = new Mock<IFiskalySettingsService>();
        settings.Setup(s => s.GetSettings()).Returns(new FiskalySettingsDto
        {
            Enabled = true,
            Environment = "TEST",
            IsConfigured = false
        });
        var controller = new AdminFiskalyController(settings.Object, Mock.Of<IFiskalySetupService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = controller.GetSettings();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<FiskalySettingsDto>(ok.Value);
        Assert.True(dto.Enabled);
    }

    [Fact]
    public async Task UpdateSettings_NullBody_Returns400()
    {
        var controller = new AdminFiskalyController(Mock.Of<IFiskalySettingsService>(), Mock.Of<IFiskalySetupService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "sa-1")], "Test"))
                }
            }
        };

        var result = await controller.UpdateSettings(null);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void SettingsEndpoints_RequireCashRegisterManage()
    {
        var get = typeof(AdminFiskalyController).GetMethod(nameof(AdminFiskalyController.GetSettings));
        var post = typeof(AdminFiskalyController).GetMethod(nameof(AdminFiskalyController.UpdateSettings));
        Assert.NotNull(get);
        Assert.NotNull(post);
        Assert.Contains(
            AppPermissions.CashRegisterManage,
            get!.GetCustomAttributes<HasPermissionAttribute>().Select(a => a.Permission));
        Assert.Contains(
            AppPermissions.CashRegisterManage,
            post!.GetCustomAttributes<HasPermissionAttribute>().Select(a => a.Permission));
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.CashRegisterManage));
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.CashRegisterManage));
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.CashRegisterManage));
    }

    [Fact]
    public void SetupEndpoints_RequireSystemCritical()
    {
        var setup = typeof(AdminFiskalyController).GetMethod(nameof(AdminFiskalyController.GetSetup));
        var fon = typeof(AdminFiskalyController).GetMethod(nameof(AdminFiskalyController.AuthenticateFon));
        var scu = typeof(AdminFiskalyController).GetMethod(nameof(AdminFiskalyController.InitializeScu));
        var cr = typeof(AdminFiskalyController).GetMethod(nameof(AdminFiskalyController.InitializeCashRegister));
        Assert.NotNull(setup);
        Assert.NotNull(fon);
        Assert.NotNull(scu);
        Assert.NotNull(cr);
        Assert.Contains(AppPermissions.SystemCritical, setup!.GetCustomAttributes<HasPermissionAttribute>().Select(a => a.Permission));
        Assert.Contains(AppPermissions.SystemCritical, fon!.GetCustomAttributes<HasPermissionAttribute>().Select(a => a.Permission));
        Assert.Contains(AppPermissions.SystemCritical, scu!.GetCustomAttributes<HasPermissionAttribute>().Select(a => a.Permission));
        Assert.Contains(AppPermissions.SystemCritical, cr!.GetCustomAttributes<HasPermissionAttribute>().Select(a => a.Permission));
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.SystemCritical));
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.SystemCritical));
    }

    [Fact]
    public async Task AuthenticateFon_NullBody_Returns400()
    {
        var controller = new AdminFiskalyController(Mock.Of<IFiskalySettingsService>(), Mock.Of<IFiskalySetupService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.AuthenticateFon(null, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
