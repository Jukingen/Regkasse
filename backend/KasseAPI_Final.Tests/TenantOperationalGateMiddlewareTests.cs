using System.Security.Claims;
using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantOperationalGateMiddlewareTests
{
    private static readonly Guid TenantId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly DateTime Now = new(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc);

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tenant_gate_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedTenantAsync(AppDbContext db, DateTime? validUntilUtc)
    {
        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Name = "Gate Tenant",
            Slug = "gate-tenant",
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseValidUntilUtc = validUntilUtc,
            CreatedAt = Now,
        });
        await db.SaveChangesAsync();
    }

    private static DefaultHttpContext CreateHttpContext(string path, string method, bool superAdmin = false)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();

        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "u1"));
        identity.AddClaim(new Claim(ClaimTypes.Role, superAdmin ? Roles.SuperAdmin : Roles.Manager));
        context.User = new ClaimsPrincipal(identity);
        return context;
    }

    private static async Task<string> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task InvokeAsync_GraceReadOnly_BlocksNonUserManagementWrites()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, Now.AddDays(-45));

        var accessor = new CurrentTenantAccessor { TenantId = TenantId };
        var context = CreateHttpContext("/api/admin/products", HttpMethods.Post);
        var nextCalled = false;
        var sut = new TenantOperationalGateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await sut.InvokeAsync(
            context,
            accessor,
            db,
            Mock.Of<ILogger<TenantOperationalGateMiddleware>>(),
            new TenantLicenseValidator());

        var body = await ReadBodyAsync(context);
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Contains("LICENSE_EXPIRED_WRITE_BLOCKED", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_GraceReadOnly_AllowsUserManagementWrites()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, Now.AddDays(-45));

        var accessor = new CurrentTenantAccessor { TenantId = TenantId };
        var context = CreateHttpContext("/api/admin/users", HttpMethods.Post);
        var nextCalled = false;
        var sut = new TenantOperationalGateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await sut.InvokeAsync(
            context,
            accessor,
            db,
            Mock.Of<ILogger<TenantOperationalGateMiddleware>>(),
            new TenantLicenseValidator());

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_GraceWrite_AddsWarningHeaders()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, Now.AddDays(-5));

        var accessor = new CurrentTenantAccessor { TenantId = TenantId };
        var context = CreateHttpContext("/api/admin/products", HttpMethods.Get);
        var nextCalled = false;
        var sut = new TenantOperationalGateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await sut.InvokeAsync(
            context,
            accessor,
            db,
            Mock.Of<ILogger<TenantOperationalGateMiddleware>>(),
            new TenantLicenseValidator());

        Assert.True(nextCalled);
        Assert.Equal("expired_grace", context.Response.Headers[TenantOperationalGateMiddleware.TenantLicenseWarningHeaderName].ToString());
        Assert.Equal("5", context.Response.Headers[TenantOperationalGateMiddleware.LicenseDaysExpiredHeaderName].ToString());
    }

    [Fact]
    public async Task InvokeAsync_Lockdown_AllowsGetRequests_ForNormalUser()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, Now.AddDays(-120));
        var normalContext = CreateHttpContext("/api/admin/products", HttpMethods.Get);
        var nextCalled = false;
        var sut = new TenantOperationalGateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await sut.InvokeAsync(
            normalContext,
            new CurrentTenantAccessor { TenantId = TenantId },
            db,
            Mock.Of<ILogger<TenantOperationalGateMiddleware>>(),
            new TenantLicenseValidator());
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_Lockdown_BlocksPostForNormalUser_ButAllowsSuperAdmin()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, Now.AddDays(-120));

        var normalContext = CreateHttpContext("/api/admin/products", HttpMethods.Post);
        var sut = new TenantOperationalGateMiddleware(_ => Task.CompletedTask);

        await sut.InvokeAsync(
            normalContext,
            new CurrentTenantAccessor { TenantId = TenantId },
            db,
            Mock.Of<ILogger<TenantOperationalGateMiddleware>>(),
            new TenantLicenseValidator());
        var normalBody = await ReadBodyAsync(normalContext);
        Assert.Equal(StatusCodes.Status403Forbidden, normalContext.Response.StatusCode);
        Assert.Contains("LICENSE_EXPIRED_WRITE_BLOCKED", normalBody, StringComparison.Ordinal);

        var superContext = CreateHttpContext("/api/admin/products", HttpMethods.Post, superAdmin: true);
        var nextCalled = false;
        var superSut = new TenantOperationalGateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        await superSut.InvokeAsync(
            superContext,
            new CurrentTenantAccessor { TenantId = TenantId },
            db,
            Mock.Of<ILogger<TenantOperationalGateMiddleware>>(),
            new TenantLicenseValidator());
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_GraceReadOnly_AllowsReportExportPosts()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, Now.AddDays(-45));

        var accessor = new CurrentTenantAccessor { TenantId = TenantId };
        var context = CreateHttpContext("/api/reports/export", HttpMethods.Post);
        var nextCalled = false;
        var sut = new TenantOperationalGateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await sut.InvokeAsync(
            context,
            accessor,
            db,
            Mock.Of<ILogger<TenantOperationalGateMiddleware>>(),
            new TenantLicenseValidator());

        Assert.True(nextCalled);
    }
}
