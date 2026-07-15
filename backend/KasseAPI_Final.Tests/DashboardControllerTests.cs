using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services.Dashboard;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DashboardControllerTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Dashboard_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static DashboardController CreateController(
        AppDbContext db,
        string userId = "user-a",
        string role = Roles.Manager)
    {
        var tenantResolver = new Mock<ISettingsTenantResolver>();
        tenantResolver
            .Setup(r => r.ResolveEffectiveTenantIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(TenantA);

        var controller = new DashboardController(
            db,
            tenantResolver.Object,
            Mock.Of<ILogger<DashboardController>>());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role),
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) },
        };

        return controller;
    }

    [Fact]
    public async Task GetPreferences_WhenNoneStored_ReturnsDefaultLayoutForRole()
    {
        await using var db = CreateContext();
        var controller = CreateController(db);

        var result = await controller.GetPreferences(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<DashboardPreferencesResponseDto>(ok.Value);

        Assert.Null(body.UpdatedAtUtc);
        Assert.Contains(body.Widgets, w => w.WidgetId == "today-sales" && w.IsVisible);
        Assert.Contains(body.Widgets, w => w.WidgetId == "active-cash-registers");
    }

    [Fact]
    public async Task SavePreferences_PersistsPerUserAndTenant()
    {
        await using var db = CreateContext();
        var controllerA = CreateController(db, "user-a");
        var controllerB = CreateController(db, "user-b");

        var saveA = await controllerA.SavePreferences(new SaveDashboardPreferencesRequestDto
        {
            Widgets =
            [
                new DashboardWidgetPreferenceDto { WidgetId = "today-sales", Order = 0, IsVisible = true },
                new DashboardWidgetPreferenceDto { WidgetId = "active-cash-registers", Order = 1, IsVisible = false },
            ],
        }, CancellationToken.None);
        Assert.IsType<OkObjectResult>(saveA.Result);

        var saveB = await controllerB.SavePreferences(new SaveDashboardPreferencesRequestDto
        {
            Widgets =
            [
                new DashboardWidgetPreferenceDto { WidgetId = "today-sales", Order = 0, IsVisible = false },
            ],
        }, CancellationToken.None);
        Assert.IsType<OkObjectResult>(saveB.Result);

        var prefsA = Assert.IsType<DashboardPreferencesResponseDto>(
            Assert.IsType<OkObjectResult>((await controllerA.GetPreferences(CancellationToken.None)).Result!).Value);
        var prefsB = Assert.IsType<DashboardPreferencesResponseDto>(
            Assert.IsType<OkObjectResult>((await controllerB.GetPreferences(CancellationToken.None)).Result!).Value);

        var salesA = prefsA.Widgets.First(w => w.WidgetId == "today-sales");
        var salesB = prefsB.Widgets.First(w => w.WidgetId == "today-sales");
        Assert.True(salesA.IsVisible);
        Assert.False(salesB.IsVisible);
    }

    [Fact]
    public async Task GetWidgets_Manager_IncludesBackupAndOfflineWidgets()
    {
        await using var db = CreateContext();
        var controller = CreateController(db, role: Roles.Manager);

        var result = await controller.GetWidgets(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<DashboardWidgetCatalogItemDto>>(ok.Value);

        Assert.Contains(items, i => i.WidgetId == DashboardWidgetCatalog.BackupStatus);
        Assert.Contains(items, i => i.WidgetId == DashboardWidgetCatalog.OfflineSystemStatus);
        Assert.Contains(items, i => i.WidgetId == DashboardWidgetCatalog.LicenseExpiry);
    }

    [Fact]
    public async Task GetWidgets_FiltersByManagerPermissions()
    {
        await using var db = CreateContext();
        var controller = CreateController(db, role: Roles.Cashier);

        var result = await controller.GetWidgets(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<DashboardWidgetCatalogItemDto>>(ok.Value);

        Assert.DoesNotContain(items, i => i.WidgetId == "today-sales");
        Assert.Contains(items, i => i.WidgetId == "active-cash-registers");
    }

    [Fact]
    public async Task SavePreferences_StoresTopProductsPeriodSetting()
    {
        await using var db = CreateContext();
        var controller = CreateController(db);

        await controller.SavePreferences(new SaveDashboardPreferencesRequestDto
        {
            Widgets =
            [
                new DashboardWidgetPreferenceDto
                {
                    WidgetId = "top-selling-products",
                    Order = 0,
                    IsVisible = true,
                    Settings = new Dictionary<string, JsonElement>
                    {
                        ["period"] = JsonSerializer.SerializeToElement("week"),
                    },
                },
            ],
        }, CancellationToken.None);

        var row = await db.DashboardPreferences.SingleAsync();
        var widget = row.Widgets.First(w => w.WidgetId == "top-selling-products");
        Assert.Equal("week", widget.Settings!["period"].GetString());
    }
}
