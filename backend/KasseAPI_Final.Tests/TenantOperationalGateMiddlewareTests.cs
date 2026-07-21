using System.Security.Claims;
using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

[Collection("OpenApiExportWebHost")]
public sealed class TenantOperationalGateMiddlewareTests
{
    public TenantOperationalGateMiddlewareTests()
    {
        OpenApiExportHostGate.EnsureExportModeDisabled();
    }

    private static readonly Guid TenantId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly DateTime Now = new(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc);

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tenant_gate_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
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

    private static IHostEnvironment CreateHostEnvironment(bool isDevelopment) =>
        Mock.Of<IHostEnvironment>(e => e.EnvironmentName == (isDevelopment ? Environments.Development : Environments.Production));

    private static IOptions<TseOptions> CreateTseOptions(string tseMode = "Device") =>
        Options.Create(new TseOptions { TseMode = tseMode });

    private static IDevelopmentModeService CreateDevelopmentMode(bool bypassLicense = false) =>
        Mock.Of<IDevelopmentModeService>(d => d.ShouldBypassLicense() == bypassLicense);

    private static IOptions<LicenseOptions> CreateLicenseOptions(bool enabled = true) =>
        Options.Create(new LicenseOptions { Enabled = enabled });

    private static Task InvokeGateAsync(
        TenantOperationalGateMiddleware sut,
        HttpContext context,
        ICurrentTenantAccessor accessor,
        AppDbContext db,
        bool isDevelopment = false,
        string tseMode = "Device",
        bool licenseEnabled = true) =>
        sut.InvokeAsync(
            context,
            accessor,
            db,
            Mock.Of<ILogger<TenantOperationalGateMiddleware>>(),
            new TenantLicenseValidator(),
            CreateHostEnvironment(isDevelopment),
            CreateTseOptions(tseMode),
            CreateLicenseOptions(licenseEnabled),
            CreateDevelopmentMode(),
            LocalizationTestDoubles.ApiMessageLocalizer());

    [Fact]
    public async Task InvokeAsync_LockdownAfterGrace_BlocksNonUserManagementWrites()
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

        await InvokeGateAsync(sut, context, accessor, db);

        var body = await ReadBodyAsync(context);
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Contains("LICENSE_EXPIRED_WRITE_BLOCKED", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_LockdownAfterGrace_BlocksUserManagementWrites()
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

        await InvokeGateAsync(sut, context, accessor, db);

        var body = await ReadBodyAsync(context);
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Contains("LICENSE_EXPIRED_USER_MGMT_BLOCKED", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_GraceWrite_AddsWarningHeaders()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, DateTime.UtcNow.AddDays(-5));

        var accessor = new CurrentTenantAccessor { TenantId = TenantId };
        var context = CreateHttpContext("/api/admin/products", HttpMethods.Get);
        var nextCalled = false;
        var sut = new TenantOperationalGateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await InvokeGateAsync(sut, context, accessor, db);

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

        await InvokeGateAsync(
            sut,
            normalContext,
            new CurrentTenantAccessor { TenantId = TenantId },
            db);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_Lockdown_BlocksPostForNormalUser_ButAllowsSuperAdmin()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, Now.AddDays(-120));

        var normalContext = CreateHttpContext("/api/admin/products", HttpMethods.Post);
        var sut = new TenantOperationalGateMiddleware(_ => Task.CompletedTask);

        await InvokeGateAsync(
            sut,
            normalContext,
            new CurrentTenantAccessor { TenantId = TenantId },
            db);
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
        await InvokeGateAsync(
            superSut,
            superContext,
            new CurrentTenantAccessor { TenantId = TenantId },
            db);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_LockdownAfterGrace_AllowsReportExportPosts()
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

        await InvokeGateAsync(sut, context, accessor, db);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_Development_SkipsTenantLicenseEnforcement()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, Now.AddDays(-120));

        var accessor = new CurrentTenantAccessor { TenantId = TenantId };
        var context = CreateHttpContext("/api/admin/products", HttpMethods.Post);
        var nextCalled = false;
        var sut = new TenantOperationalGateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await InvokeGateAsync(sut, context, accessor, db, isDevelopment: true);

        Assert.True(nextCalled);
    }
}
