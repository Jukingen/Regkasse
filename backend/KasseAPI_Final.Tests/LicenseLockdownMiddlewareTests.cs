using System.Security.Claims;
using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.License;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

[Collection("OpenApiExportWebHost")]
public sealed class LicenseLockdownMiddlewareTests
{
    public LicenseLockdownMiddlewareTests()
    {
        OpenApiExportHostGate.EnsureExportModeDisabled();
    }

    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static DefaultHttpContext CreateContext(
        string path,
        string method,
        string? appContext = ClientAppPolicy.Admin,
        bool superAdmin = false)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();

        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "u1"));
        identity.AddClaim(new Claim(ClaimTypes.Role, superAdmin ? Roles.SuperAdmin : Roles.Manager));
        if (!string.IsNullOrEmpty(appContext))
            identity.AddClaim(new Claim(ClientAppPolicy.AppContextClaimType, appContext));
        context.User = new ClaimsPrincipal(identity);
        return context;
    }

    private static Mock<ILicenseService> CreateLicenseService(LicenseStatusInfo tenantStatus)
    {
        var mock = new Mock<ILicenseService>(MockBehavior.Loose);
        mock.Setup(x => x.GetLicenseStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantStatus);
        return mock;
    }

    private static ICurrentTenantAccessor CreateTenantAccessor(Guid? tenantId = null) =>
        new CurrentTenantAccessor { TenantId = tenantId ?? TenantId };

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

    private static Task InvokeAsync(
        LicenseLockdownMiddleware sut,
        DefaultHttpContext context,
        Mock<ILicenseService> licenseService,
        bool isDevelopment = false,
        bool licenseEnabled = true,
        Guid? tenantId = null) =>
        sut.InvokeAsync(
            context,
            licenseService.Object,
            CreateTenantAccessor(tenantId),
            CreateHostEnvironment(isDevelopment),
            CreateTseOptions(),
            CreateLicenseOptions(licenseEnabled),
            CreateDevelopmentMode());

    private static LicenseStatusInfo LockedStatus(int daysOverdue = 10) =>
        new()
        {
            IsActive = false,
            IsExpired = true,
            IsInGracePeriod = false,
            IsLocked = true,
            CanAccess = false,
            CanTransact = false,
            DaysOverdue = daysOverdue,
            ValidUntil = DateTime.UtcNow.AddDays(-daysOverdue),
            LockDate = DateTime.UtcNow.AddDays(-daysOverdue + LicenseGracePeriodConfig.GracePeriodDays),
            RequiresRenewal = true,
            StatusMessage = "Lizenz gesperrt",
        };

    private static LicenseStatusInfo GraceStatus() =>
        new()
        {
            IsActive = false,
            IsExpired = true,
            IsInGracePeriod = true,
            IsLocked = false,
            CanAccess = true,
            CanTransact = true,
            DaysOverdue = 3,
            GracePeriodRemaining = 4,
            ValidUntil = DateTime.UtcNow.AddDays(-3),
            LockDate = DateTime.UtcNow.AddDays(4),
            StatusMessage = "Grace",
        };

    private static LicenseStatusInfo ActiveStatus() =>
        new()
        {
            IsActive = true,
            IsExpired = false,
            IsInGracePeriod = false,
            IsLocked = false,
            CanAccess = true,
            CanTransact = true,
            DaysRemaining = 30,
            DaysOverdue = 0,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            StatusMessage = "Active",
        };

    [Fact]
    public async Task InvokeAsync_NonFaRequest_SkipsGate()
    {
        var nextCalled = false;
        var sut = new LicenseLockdownMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            Mock.Of<ILogger<LicenseLockdownMiddleware>>());

        var context = CreateContext("/api/pos/cart", "POST", appContext: ClientAppPolicy.Pos);
        var license = CreateLicenseService(LockedStatus());

        await InvokeAsync(sut, context, license);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ActiveLicense_AllowsWrite()
    {
        var nextCalled = false;
        var sut = new LicenseLockdownMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            Mock.Of<ILogger<LicenseLockdownMiddleware>>());

        var context = CreateContext("/api/admin/products", "POST");
        await InvokeAsync(sut, context, CreateLicenseService(ActiveStatus()));

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_GraceLicense_AllowsWrite()
    {
        var nextCalled = false;
        var sut = new LicenseLockdownMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            Mock.Of<ILogger<LicenseLockdownMiddleware>>());

        var context = CreateContext("/api/admin/products", "PUT");
        await InvokeAsync(sut, context, CreateLicenseService(GraceStatus()));

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_Locked_AllowsGet()
    {
        var nextCalled = false;
        var sut = new LicenseLockdownMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            Mock.Of<ILogger<LicenseLockdownMiddleware>>());

        var context = CreateContext("/api/admin/products", "GET");
        await InvokeAsync(sut, context, CreateLicenseService(LockedStatus()));

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_Locked_BlocksProductWrite()
    {
        var nextCalled = false;
        var sut = new LicenseLockdownMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            Mock.Of<ILogger<LicenseLockdownMiddleware>>());

        var context = CreateContext("/api/admin/products", "POST");
        await InvokeAsync(sut, context, CreateLicenseService(LockedStatus()));

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        var body = await ReadBodyAsync(context);
        Assert.Contains(LicenseLockdownMiddleware.LicenseExpiredCode, body, StringComparison.Ordinal);
        Assert.Contains(nameof(LicenseLifecycleState.Locked), body, StringComparison.Ordinal);
        Assert.Contains("Write operations are disabled", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_Archived_BlocksWrite_AllowsLicenseExtend()
    {
        var nextCalled = false;
        var sut = new LicenseLockdownMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            Mock.Of<ILogger<LicenseLockdownMiddleware>>());

        var archived = LockedStatus(daysOverdue: LicenseGracePeriodConfig.ArchiveAfterDays + 5);
        var blocked = CreateContext("/api/admin/products", "DELETE");
        await InvokeAsync(sut, blocked, CreateLicenseService(archived));
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, blocked.Response.StatusCode);
        var body = await ReadBodyAsync(blocked);
        Assert.Contains(nameof(LicenseLifecycleState.Archived), body, StringComparison.Ordinal);

        nextCalled = false;
        var extend = CreateContext("/api/admin/license/extend", "POST");
        await InvokeAsync(sut, extend, CreateLicenseService(archived));
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_Locked_AllowsDataManagementWrite()
    {
        var nextCalled = false;
        var sut = new LicenseLockdownMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            Mock.Of<ILogger<LicenseLockdownMiddleware>>());

        var context = CreateContext($"/api/admin/tenants/{TenantId}/data-management/requests", "POST");
        await InvokeAsync(sut, context, CreateLicenseService(LockedStatus()));

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_Locked_AllowsBillingActivate()
    {
        var nextCalled = false;
        var sut = new LicenseLockdownMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            Mock.Of<ILogger<LicenseLockdownMiddleware>>());

        var context = CreateContext("/api/license/billing/activate", "POST");
        await InvokeAsync(sut, context, CreateLicenseService(LockedStatus()));

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_Locked_SuperAdminBypasses()
    {
        var nextCalled = false;
        var sut = new LicenseLockdownMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            Mock.Of<ILogger<LicenseLockdownMiddleware>>());

        var context = CreateContext("/api/admin/products", "POST", superAdmin: true);
        await InvokeAsync(sut, context, CreateLicenseService(LockedStatus()));

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_Development_SkipsEnforcement()
    {
        var nextCalled = false;
        var sut = new LicenseLockdownMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            Mock.Of<ILogger<LicenseLockdownMiddleware>>());

        var context = CreateContext("/api/admin/products", "POST");
        await InvokeAsync(sut, context, CreateLicenseService(LockedStatus()), isDevelopment: true);

        Assert.True(nextCalled);
    }

    [Fact]
    public void ResolveLifecycleState_MapsActiveGraceLockedArchived()
    {
        Assert.Equal(LicenseLifecycleState.Active, LicenseLockdownMiddleware.ResolveLifecycleState(ActiveStatus()));
        Assert.Equal(LicenseLifecycleState.Grace, LicenseLockdownMiddleware.ResolveLifecycleState(GraceStatus()));
        Assert.Equal(LicenseLifecycleState.Locked, LicenseLockdownMiddleware.ResolveLifecycleState(LockedStatus(10)));
        Assert.Equal(
            LicenseLifecycleState.Archived,
            LicenseLockdownMiddleware.ResolveLifecycleState(
                LockedStatus(LicenseGracePeriodConfig.ArchiveAfterDays + 1)));
    }

    [Fact]
    public void IsFaRequest_DetectsAdminContextAndAdminPath()
    {
        Assert.True(LicenseLockdownMiddleware.IsFaRequest(CreateContext("/api/admin/users", "GET")));
        Assert.True(LicenseLockdownMiddleware.IsFaRequest(
            CreateContext("/api/license/billing/status", "GET", appContext: ClientAppPolicy.Admin)));
        Assert.False(LicenseLockdownMiddleware.IsFaRequest(
            CreateContext("/api/pos/cart", "GET", appContext: ClientAppPolicy.Pos)));
    }

    [Fact]
    public async Task InvokeAsync_Unauthenticated_SkipsGate()
    {
        var nextCalled = false;
        var sut = new LicenseLockdownMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            Mock.Of<ILogger<LicenseLockdownMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/admin/products";
        context.Request.Method = "POST";
        context.User = new ClaimsPrincipal(new ClaimsIdentity()); // not authenticated

        await InvokeAsync(sut, context, CreateLicenseService(LockedStatus()));

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/license/extend", true)]
    [InlineData("/api/admin/license/activate", true)]
    [InlineData("/api/license/billing/activate", true)]
    [InlineData("/api/license/activate", true)]
    [InlineData("/api/license/validate", true)]
    [InlineData("/api/license/info", true)]
    [InlineData("/api/admin/tenants/11111111-1111-1111-1111-111111111111/data-management/closure", true)]
    [InlineData("/api/admin/products", false)]
    [InlineData("/api/admin/users", false)]
    [InlineData("/api/admin/rksv/dep-export", false)]
    public void IsAllowedWriteOperation_MatchesRenewalAndDataManagement(string path, bool allowed)
    {
        Assert.Equal(allowed, LicenseLockdownMiddleware.IsAllowedWriteOperation(path));
    }
}
