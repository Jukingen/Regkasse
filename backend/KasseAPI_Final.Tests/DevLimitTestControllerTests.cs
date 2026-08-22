using System.Reflection;
using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Limits;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DevLimitTestControllerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static DevLimitTestController CreateController(
        bool isDevelopment,
        ITenantLimitService? limits = null,
        ITenantLimitGuard? guard = null,
        ITenantLimitCacheService? cache = null)
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName)
            .Returns(isDevelopment ? Environments.Development : Environments.Production);

        var controller = new DevLimitTestController(
            env.Object,
            limits ?? Mock.Of<ITenantLimitService>(),
            guard ?? Mock.Of<ITenantLimitGuard>(),
            cache ?? Mock.Of<ITenantLimitCacheService>(),
            NullLogger<DevLimitTestController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "root"),
                        new Claim(ClaimTypes.Role, Roles.SuperAdmin),
                    ],
                    "Test")),
            },
        };
        return controller;
    }

    private static TenantLimitUsageDto SampleUsage()
    {
        var row = TenantLimits.CreateDefault(TenantId);
        return new TenantLimitUsageDto
        {
            TenantId = TenantId,
            Limits = TenantLimitsDto.FromEntity(row),
            CurrentProducts = 4,
            CurrentUsers = 2,
            CurrentDailyTransactions = 0,
            CurrentDailyRevenue = 0m,
            CurrentBackups = 0,
            CurrentBackupSizeMb = 0m,
            CurrentOfflineTransactions = 0,
            CurrentMaxAssignedRegistersPerUser = 1,
        };
    }

    [Fact]
    public void Controller_RequiresSuperAdminRole()
    {
        var attr = typeof(DevLimitTestController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(Roles.SuperAdmin, attr.Roles);
    }

    [Fact]
    public async Task SetLimit_OutsideDevelopment_ReturnsNotFound()
    {
        var result = await CreateController(isDevelopment: false).SetLimit(new SetLimitRequest
        {
            TenantId = TenantId,
            LimitKey = TenantLimitKeys.MaxProductsPerTenant,
            Value = 10,
        });

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task ResetAll_OutsideDevelopment_ReturnsNotFound()
    {
        var result = await CreateController(isDevelopment: false).ResetAllLimits(TenantId);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task TriggerScenario_OutsideDevelopment_ReturnsNotFound()
    {
        var result = await CreateController(isDevelopment: false).TriggerLimitScenario(
            new TriggerLimitScenarioRequest
            {
                TenantId = TenantId,
                Scenario = DevLimitScenarioNames.Tiny,
            });
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task ClearCache_OutsideDevelopment_ReturnsNotFound()
    {
        var result = await CreateController(isDevelopment: false).ClearLimitCache(TenantId);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetStatus_OutsideDevelopment_ReturnsNotFound()
    {
        var result = await CreateController(isDevelopment: false).GetStatus(TenantId);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task SetLimit_InDevelopment_PersistsAndReturnsUsage()
    {
        var limits = new Mock<ITenantLimitService>();
        limits
            .Setup(s => s.SetLimitValueAsync(
                TenantId, TenantLimitKeys.MaxProductsPerTenant, 12m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TenantLimits.CreateDefault(TenantId));
        var guard = new Mock<ITenantLimitGuard>();
        guard.Setup(g => g.GetUsageAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleUsage());

        var result = await CreateController(true, limits.Object, guard.Object).SetLimit(new SetLimitRequest
        {
            TenantId = TenantId,
            LimitKey = TenantLimitKeys.MaxProductsPerTenant,
            Value = 12,
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<TenantLimitUsageDto>(ok.Value);
        limits.Verify(
            s => s.SetLimitValueAsync(
                TenantId, TenantLimitKeys.MaxProductsPerTenant, 12m, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetAll_InDevelopment_ResetsViaService()
    {
        var limits = new Mock<ITenantLimitService>();
        limits.Setup(s => s.ResetLimitsAsync(TenantId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var guard = new Mock<ITenantLimitGuard>();
        guard.Setup(g => g.GetUsageAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleUsage());

        var result = await CreateController(true, limits.Object, guard.Object).ResetAllLimits(TenantId);

        Assert.IsType<OkObjectResult>(result.Result);
        limits.Verify(s => s.ResetLimitsAsync(TenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TriggerScenario_Tiny_UpdatesAllCaps()
    {
        var limits = new Mock<ITenantLimitService>();
        limits
            .Setup(s => s.UpdateLimitsAsync(TenantId, It.IsAny<UpdateTenantLimitsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TenantLimits.CreateDefault(TenantId));
        var guard = new Mock<ITenantLimitGuard>();
        guard.Setup(g => g.GetUsageAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleUsage());

        var result = await CreateController(true, limits.Object, guard.Object).TriggerLimitScenario(
            new TriggerLimitScenarioRequest
            {
                TenantId = TenantId,
                Scenario = DevLimitScenarioNames.Tiny,
            });

        Assert.IsType<OkObjectResult>(result.Result);
        limits.Verify(
            s => s.UpdateLimitsAsync(
                TenantId,
                It.Is<UpdateTenantLimitsRequest>(r => r.MaxProductsPerTenant == 1 && r.MaxUsersPerTenant == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TriggerScenario_Reset_CallsResetLimits()
    {
        var limits = new Mock<ITenantLimitService>();
        limits.Setup(s => s.ResetLimitsAsync(TenantId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var guard = new Mock<ITenantLimitGuard>();
        guard.Setup(g => g.GetUsageAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleUsage());

        var result = await CreateController(true, limits.Object, guard.Object).TriggerLimitScenario(
            new TriggerLimitScenarioRequest
            {
                TenantId = TenantId,
                Scenario = DevLimitScenarioNames.Reset,
            });

        Assert.IsType<OkObjectResult>(result.Result);
        limits.Verify(s => s.ResetLimitsAsync(TenantId, It.IsAny<CancellationToken>()), Times.Once);
        limits.Verify(
            s => s.UpdateLimitsAsync(It.IsAny<Guid>(), It.IsAny<UpdateTenantLimitsRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ClearCache_InDevelopment_InvalidatesThenReturnsUsage()
    {
        var cache = new Mock<ITenantLimitCacheService>();
        cache.Setup(c => c.InvalidateAsync(TenantId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var guard = new Mock<ITenantLimitGuard>();
        guard.Setup(g => g.GetUsageAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleUsage());

        var result = await CreateController(true, cache: cache.Object, guard: guard.Object)
            .ClearLimitCache(TenantId);

        Assert.IsType<OkObjectResult>(result.Result);
        cache.Verify(c => c.InvalidateAsync(TenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetLimit_UnknownTenant_Returns404()
    {
        var limits = new Mock<ITenantLimitService>();
        limits
            .Setup(s => s.SetLimitValueAsync(
                TenantId, TenantLimitKeys.MaxUsersPerTenant, 3m, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Tenant not found."));

        var result = await CreateController(true, limits.Object).SetLimit(new SetLimitRequest
        {
            TenantId = TenantId,
            LimitKey = TenantLimitKeys.MaxUsersPerTenant,
            Value = 3,
        });

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }
}
