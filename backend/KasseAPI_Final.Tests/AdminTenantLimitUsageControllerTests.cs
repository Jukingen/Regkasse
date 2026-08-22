using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Limits;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminTenantLimitUsageControllerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task GetUsage_WithoutTenantContext_Returns404()
    {
        var guard = new Mock<ITenantLimitGuard>();
        var dashboard = new Mock<ITenantLimitDashboardService>();
        var controller = new AdminTenantLimitUsageController(
            guard.Object,
            dashboard.Object,
            NullCurrentTenantAccessor.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.GetUsage();

        Assert.IsType<NotFoundObjectResult>(result.Result);
        guard.Verify(g => g.GetUsageAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetUsage_ReturnsDto()
    {
        var usage = new TenantLimitUsageDto
        {
            TenantId = TenantId,
            Limits = TenantLimitsDto.FromEntity(TenantLimits.CreateDefault(TenantId)),
            CurrentProducts = 3,
            CurrentUsers = 2,
            CurrentDailyTransactions = 4,
            CurrentDailyRevenue = 40m,
            CurrentBackups = 0,
            CurrentBackupSizeMb = 0,
            CurrentOfflineTransactions = 0,
            CurrentMaxAssignedRegistersPerUser = 0,
        };
        var guard = new Mock<ITenantLimitGuard>();
        guard.Setup(g => g.GetUsageAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(usage);

        var controller = new AdminTenantLimitUsageController(
            guard.Object,
            Mock.Of<ITenantLimitDashboardService>(),
            TenantTestDoubles.TenantAccessorReturning(TenantId))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.Role, "Manager")],
                        "Test")),
                },
            },
        };

        var result = await controller.GetUsage();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TenantLimitUsageDto>(ok.Value);
        Assert.Equal(3, dto.CurrentProducts);
        Assert.Equal(2, dto.CurrentUsers);
    }

    [Fact]
    public async Task GetDashboard_ManagerWithoutTenant_Returns404()
    {
        var dashboard = new Mock<ITenantLimitDashboardService>();
        var controller = new AdminTenantLimitUsageController(
            Mock.Of<ITenantLimitGuard>(),
            dashboard.Object,
            NullCurrentTenantAccessor.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.Role, Roles.Manager)],
                        "Test")),
                },
            },
        };

        var result = await controller.GetDashboard();

        Assert.IsType<NotFoundObjectResult>(result.Result);
        dashboard.Verify(
            d => d.GetDashboardAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetDashboard_SuperAdminWithoutTenant_LoadsAllTenants()
    {
        var dashboard = new Mock<ITenantLimitDashboardService>();
        dashboard
            .Setup(d => d.GetDashboardForAllTenantsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitDashboardDto { AllTenants = true, ApproachingLimits = 2 });

        var controller = new AdminTenantLimitUsageController(
            Mock.Of<ITenantLimitGuard>(),
            dashboard.Object,
            NullCurrentTenantAccessor.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, "sa-1"),
                            new Claim(ClaimTypes.Role, Roles.SuperAdmin),
                        ],
                        "Test")),
                },
            },
        };

        var result = await controller.GetDashboard();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<LimitDashboardDto>(ok.Value);
        Assert.True(dto.AllTenants);
        Assert.Equal(2, dto.ApproachingLimits);
    }

    [Fact]
    public async Task GetDashboard_ManagerWithTenant_LoadsAmbient()
    {
        var dashboard = new Mock<ITenantLimitDashboardService>();
        dashboard
            .Setup(d => d.GetDashboardAsync(TenantId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitDashboardDto { ApproachingLimits = 1 });

        var controller = new AdminTenantLimitUsageController(
            Mock.Of<ITenantLimitGuard>(),
            dashboard.Object,
            TenantTestDoubles.TenantAccessorReturning(TenantId))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, "mgr-1"),
                            new Claim(ClaimTypes.Role, Roles.Manager),
                        ],
                        "Test")),
                },
            },
        };

        var result = await controller.GetDashboard();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<LimitDashboardDto>(ok.Value);
        Assert.Equal(1, dto.ApproachingLimits);
        dashboard.Verify(
            d => d.GetDashboardForAllTenantsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetDashboard_SuperAdminWithExplicitTenantId_LoadsThatTenant()
    {
        var dashboard = new Mock<ITenantLimitDashboardService>();
        dashboard
            .Setup(d => d.GetDashboardAsync(TenantId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitDashboardDto { ApproachingLimits = 4 });

        var controller = new AdminTenantLimitUsageController(
            Mock.Of<ITenantLimitGuard>(),
            dashboard.Object,
            NullCurrentTenantAccessor.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, "sa-1"),
                            new Claim(ClaimTypes.Role, Roles.SuperAdmin),
                        ],
                        "Test")),
                },
            },
        };

        var result = await controller.GetDashboard(allTenants: false, tenantId: TenantId);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<LimitDashboardDto>(ok.Value);
        Assert.Equal(4, dto.ApproachingLimits);
        dashboard.Verify(
            d => d.GetDashboardForAllTenantsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetDashboard_ManagerIgnoresExplicitTenantId_UsesAmbient()
    {
        var otherTenant = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var dashboard = new Mock<ITenantLimitDashboardService>();
        dashboard
            .Setup(d => d.GetDashboardAsync(TenantId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitDashboardDto { ApproachingLimits = 1 });

        var controller = new AdminTenantLimitUsageController(
            Mock.Of<ITenantLimitGuard>(),
            dashboard.Object,
            TenantTestDoubles.TenantAccessorReturning(TenantId))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, "mgr-1"),
                            new Claim(ClaimTypes.Role, Roles.Manager),
                        ],
                        "Test")),
                },
            },
        };

        var result = await controller.GetDashboard(allTenants: false, tenantId: otherTenant);

        Assert.IsType<OkObjectResult>(result.Result);
        dashboard.Verify(
            d => d.GetDashboardAsync(TenantId, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        dashboard.Verify(
            d => d.GetDashboardAsync(otherTenant, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
