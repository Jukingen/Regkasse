using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Maintenance;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class MaintenanceModeServiceTests
{
    private static (MaintenanceModeService Mode, MaintenanceNotificationService Notifications, AppDbContext Db)
        CreateServices()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"MaintMode_{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options, new FixedTenantAccessor(null));
        var notifications = new MaintenanceNotificationService(db);
        var mode = new MaintenanceModeService(db, notifications);
        return (mode, notifications, db);
    }

    private sealed class FixedTenantAccessor(Guid? tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? TenantSlug { get; set; }
    }

    [Fact]
    public async Task GetCurrentStatus_Inactive_WhenNone()
    {
        var (mode, _, _) = CreateServices();
        var status = await mode.GetCurrentStatusAsync();
        Assert.False(status.IsActive);
        Assert.False(status.BlocksPosPayments);
    }

    [Fact]
    public async Task Start_CreatesInProgressWindow()
    {
        var (mode, _, _) = CreateServices();
        var status = await mode.StartAsync("admin-1", new StartMaintenanceModeRequestDto
        {
            ScheduledEndAt = DateTime.UtcNow.AddHours(1),
            Title = "Upgrade",
            Message = "Deploying patches",
        });

        Assert.True(status.IsActive);
        Assert.True(status.BlocksApiWrites);
        Assert.True(status.BlocksPosPayments);
        Assert.Equal(MaintenanceNotificationStatuses.InProgress, status.Status);
        Assert.Equal("Upgrade", status.Title);
    }

    [Fact]
    public async Task End_ClearsActiveWindow()
    {
        var (mode, _, _) = CreateServices();
        await mode.StartAsync("admin-1", new StartMaintenanceModeRequestDto());
        var ended = await mode.EndAsync("admin-1");
        Assert.False(ended.IsActive);
    }

    [Fact]
    public async Task PublishedInWindow_IsActive()
    {
        var (mode, notifications, _) = CreateServices();
        var now = DateTime.UtcNow;
        await notifications.CreateAsync("admin-1", new CreateMaintenanceNotificationRequestDto
        {
            Title = "Scheduled",
            Message = "Window open",
            ScheduledStartAt = now.AddMinutes(-10),
            ScheduledEndAt = now.AddHours(1),
            PublishImmediately = true,
            AffectedSystems = "All",
        });

        var status = await mode.GetCurrentStatusAsync();
        Assert.True(status.IsActive);
        Assert.Equal(MaintenanceNotificationStatuses.Published, status.Status);
    }
}

public sealed class MaintenanceMiddlewareTests
{
    [Theory]
    [InlineData("/api/health")]
    [InlineData("/api/health/ready")]
    [InlineData("/health/live")]
    [InlineData("/api/Auth/login")]
    [InlineData("/api/auth/refresh")]
    [InlineData("/api/csrf/token")]
    [InlineData("/api/maintenance/status")]
    [InlineData("/api/admin/maintenance/status")]
    [InlineData("/api/pos/maintenance/status")]
    [InlineData("/api/pos/maintenance-notifications/active")]
    [InlineData("/api/admin/maintenance-notifications/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/acknowledge")]
    public void IsCriticalPath_AllowsEssentialEndpoints(string path)
    {
        Assert.True(MaintenanceMiddleware.IsCriticalPath(path));
    }

    [Theory]
    [InlineData("/api/pos/payment")]
    [InlineData("/api/admin/tenants")]
    [InlineData("/api/pos/cart")]
    public void IsCriticalPath_BlocksBusinessEndpoints(string path)
    {
        Assert.False(MaintenanceMiddleware.IsCriticalPath(path));
    }

    [Fact]
    public async Task Invoke_Returns503_WhenActive_ForBlockedWrite()
    {
        var mode = new StubModeService(active: true);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/pos/payment";
        context.Response.Body = new MemoryStream();
        var called = false;
        var mw = new MaintenanceMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }, new MaintenanceOperationFilter());

        await mw.InvokeAsync(context, mode);

        Assert.False(called);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal(MaintenanceMiddleware.LimitedModeValue, context.Response.Headers[MaintenanceMiddleware.MaintenanceModeHeaderName]);
    }

    [Fact]
    public async Task Invoke_AllowsGet_WhenActive_LimitedMode()
    {
        var mode = new StubModeService(active: true);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/admin/tenants";
        var called = false;
        var mw = new MaintenanceMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }, new MaintenanceOperationFilter());

        await mw.InvokeAsync(context, mode);

        Assert.True(called);
    }

    [Fact]
    public async Task Invoke_BlocksAdminWrite_WhenActive()
    {
        var mode = new StubModeService(active: true);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/admin/tenants";
        context.Response.Body = new MemoryStream();
        var called = false;
        var mw = new MaintenanceMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }, new MaintenanceOperationFilter());

        await mw.InvokeAsync(context, mode);

        Assert.False(called);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_AllowsSuperAdmin_WhenActive()
    {
        var mode = new StubModeService(active: true);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/pos/payment";
        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "SuperAdmin") },
                authenticationType: "test"));
        var called = false;
        var mw = new MaintenanceMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }, new MaintenanceOperationFilter());

        await mw.InvokeAsync(context, mode);

        Assert.True(called);
    }

    private sealed class StubModeService(bool active) : IMaintenanceModeService
    {
        public Task<MaintenanceModeStatusDto> GetCurrentStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new MaintenanceModeStatusDto
            {
                IsActive = active,
                ScheduledEndAt = DateTime.UtcNow.AddHours(1),
                Message = "Down for maintenance",
                Title = "Maintenance",
                BlocksApiWrites = active,
                BlocksPosPayments = active,
                Status = active ? "InProgress" : "Inactive",
            });

        public Task<MaintenanceModeStatusDto> StartAsync(
            string actorUserId,
            StartMaintenanceModeRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<MaintenanceModeStatusDto> EndAsync(
            string actorUserId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
