using KasseAPI_Final.Configuration;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Services.Operations;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OperationLimitServiceTests
{
    [Fact]
    public async Task Check_Allows_UnderLimit_And_Records()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var opts = Options.Create(new TenantOperationLimitsOptions
        {
            Enabled = true,
            MaxBulkDeletePerDay = 50,
            RequireApprovalForBulkDelete = 500,
        });
        var monitor = Mock.Of<IOptionsMonitor<TenantOperationLimitsOptions>>(m => m.CurrentValue == opts.Value);
        var svc = new OperationLimitService(cache, monitor);
        var tenantId = Guid.NewGuid();

        var check = await svc.CheckLimitAsync(tenantId, "u1", TenantOperationLimitKind.BulkDelete, 10, false);
        Assert.True(check.IsAllowed);
        Assert.Equal(50, check.Limit);

        await svc.RecordOperationAsync(tenantId, "u1", TenantOperationLimitKind.BulkDelete, 10);
        var after = await svc.CheckLimitAsync(tenantId, "u1", TenantOperationLimitKind.BulkDelete, 41, false);
        Assert.False(after.IsAllowed);
        Assert.Equal("OPERATION_LIMIT_EXCEEDED", after.Code);
    }

    [Fact]
    public async Task Check_RequiresApproval_WhenQuantityAtThreshold()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var opts = Options.Create(new TenantOperationLimitsOptions
        {
            Enabled = true,
            MaxBulkDeletePerDay = 5000,
            RequireApprovalForBulkDelete = 500,
        });
        var monitor = Mock.Of<IOptionsMonitor<TenantOperationLimitsOptions>>(m => m.CurrentValue == opts.Value);
        var svc = new OperationLimitService(cache, monitor);

        var denied = await svc.CheckLimitAsync(Guid.NewGuid(), "u1", TenantOperationLimitKind.BulkDelete, 500, false);
        Assert.False(denied.IsAllowed);
        Assert.True(denied.RequiresApproval);
        Assert.Equal("REQUIRES_APPROVAL", denied.Code);

        var allowed = await svc.CheckLimitAsync(Guid.NewGuid(), "u1", TenantOperationLimitKind.BulkDelete, 500, true);
        Assert.True(allowed.IsAllowed);
    }

    [Fact]
    public void MatchOperation_MapsKnownPaths()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var opts = Options.Create(new TenantOperationLimitsOptions());
        var monitor = Mock.Of<IOptionsMonitor<TenantOperationLimitsOptions>>(m => m.CurrentValue == opts.Value);
        var svc = new OperationLimitService(cache, monitor);

        Assert.Equal(
            TenantOperationLimitKind.BulkDelete,
            svc.MatchOperation("POST", "/api/admin/products/bulk-deactivate"));
        Assert.Equal(
            TenantOperationLimitKind.ProductCreate,
            svc.MatchOperation("POST", "/api/admin/products"));
        Assert.Equal(
            TenantOperationLimitKind.PriceUpdate,
            svc.MatchOperation("PUT", $"/api/admin/products/{Guid.NewGuid():D}"));
        Assert.Equal(
            TenantOperationLimitKind.Backup,
            svc.MatchOperation("POST", "/api/admin/backup/trigger"));
        Assert.Null(svc.MatchOperation("GET", "/api/admin/products"));
    }
}

public sealed class OperationLimitMiddlewareTests
{
    [Fact]
    public async Task Invoke_WhenDisabled_PassesThrough()
    {
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = CreateMiddleware(enabled: false, bypass: false, Environments.Production, next);
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/admin/products/bulk-deactivate";
        context.Response.Body = new MemoryStream();

        var limits = new Mock<IOperationLimitService>();
        var tenant = Mock.Of<ICurrentTenantAccessor>(a => a.TenantId == Guid.NewGuid());

        await middleware.InvokeAsync(context, limits.Object, tenant);

        Assert.True(nextCalled);
        limits.Verify(l => l.MatchOperation(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Invoke_WhenOverLimit_Returns429()
    {
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = CreateMiddleware(enabled: true, bypass: false, Environments.Production, next);
        var tenantId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/admin/products/bulk-deactivate";
        context.Request.Body = new MemoryStream("{}"u8.ToArray());
        context.Response.Body = new MemoryStream();

        var limits = new Mock<IOperationLimitService>();
        limits.Setup(l => l.MatchOperation("POST", "/api/admin/products/bulk-deactivate"))
            .Returns(TenantOperationLimitKind.BulkDelete);
        limits.Setup(l => l.CheckLimitAsync(
                tenantId,
                It.IsAny<string?>(),
                TenantOperationLimitKind.BulkDelete,
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(DTOs.OperationLimitCheckResult.Deny(
                TenantOperationLimitKind.BulkDelete,
                "OPERATION_LIMIT_EXCEEDED",
                "limit",
                50,
                50,
                DateTime.UtcNow.AddDays(1)));

        var tenant = Mock.Of<ICurrentTenantAccessor>(a => a.TenantId == tenantId);

        await middleware.InvokeAsync(context, limits.Object, tenant);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
    }

    private static OperationLimitMiddleware CreateMiddleware(
        bool enabled,
        bool bypass,
        string envName,
        RequestDelegate next)
    {
        var opts = Options.Create(new TenantOperationLimitsOptions
        {
            Enabled = enabled,
            BypassInDevelopment = bypass,
        });
        var monitor = Mock.Of<IOptionsMonitor<TenantOperationLimitsOptions>>(m => m.CurrentValue == opts.Value);
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(envName);
        return new OperationLimitMiddleware(
            next,
            NullLogger<OperationLimitMiddleware>.Instance,
            env.Object,
            monitor);
    }
}
