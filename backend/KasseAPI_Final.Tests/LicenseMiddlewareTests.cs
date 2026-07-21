using System.Text;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

[Collection("OpenApiExportWebHost")]
public sealed class LicenseMiddlewareTests
{
    public LicenseMiddlewareTests()
    {
        OpenApiExportHostGate.EnsureExportModeDisabled();
    }

    private static readonly Guid TenantId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly DateTime Now = new(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc);

    private static DefaultHttpContext CreateContext(string path, string method)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static Mock<ILicenseService> CreateLicenseService(
        LicenseStatusResponse deploymentSnapshot,
        LicenseStatusInfo? tenantStatus = null)
    {
        var mock = new Mock<ILicenseService>(MockBehavior.Loose);
        mock.Setup(x => x.ValidateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicenseValidationResult { IsLicenseOperational = true });
        mock.Setup(x => x.GetDeploymentStatus()).Returns(deploymentSnapshot);
        mock.Setup(x => x.GetStatus()).Returns(deploymentSnapshot);
        mock.Setup(x => x.GetCurrentStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(deploymentSnapshot);
        mock.Setup(x => x.GetCurrentDeploymentStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(deploymentSnapshot);
        mock.SetupGet(x => x.IsLicenseSnapshotInitialized).Returns(true);
        mock.Setup(x => x.GetLicenseStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantStatus ?? new LicenseStatusInfo
            {
                CanAccess = true,
                CanTransact = true,
                DaysRemaining = 30,
                StatusMessage = "Lizenz gültig bis 30.06.2026",
            });
        return mock;
    }

    private static ICurrentTenantAccessor CreateTenantAccessor(Guid? tenantId = null)
    {
        var accessor = new CurrentTenantAccessor { TenantId = tenantId ?? TenantId };
        return accessor;
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

    private static Task InvokeMiddlewareAsync(
        LicenseMiddleware sut,
        DefaultHttpContext context,
        Mock<ILicenseService> licenseService,
        bool isDevelopment = false,
        string tseMode = "Device",
        bool bypassLicense = false,
        bool licenseEnabled = true) =>
        sut.InvokeAsync(
            context,
            licenseService.Object,
            new DeploymentLicenseValidator(),
            CreateTenantAccessor(),
            CreateHostEnvironment(isDevelopment),
            CreateTseOptions(tseMode),
            CreateLicenseOptions(licenseEnabled),
            CreateDevelopmentMode(bypassLicense));

    [Fact]
    public async Task InvokeAsync_DeploymentGraceReadOnly_BlocksWriteRoutes()
    {
        // 20 days expired → deployment GraceReadOnly (write 15d, lockdown 60d); use UtcNow so wall-clock drift does not flake.
        var snapshot = new LicenseStatusResponse(false, false, true, 0, DateTime.UtcNow.AddDays(-20), "machine");
        var licenseService = CreateLicenseService(snapshot);
        var context = CreateContext("/api/admin/products", HttpMethods.Post);
        var nextCalled = false;
        var sut = new LicenseMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await InvokeMiddlewareAsync(sut, context, licenseService);

        var body = await ReadBodyAsync(context);
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Contains("DEPLOYMENT_LICENSE_READ_ONLY", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_DeploymentGraceReadOnly_AllowsAuthWrites()
    {
        var snapshot = new LicenseStatusResponse(false, false, true, 0, DateTime.UtcNow.AddDays(-20), "machine");
        var licenseService = CreateLicenseService(snapshot);
        var context = CreateContext("/api/auth/login", HttpMethods.Post);
        var nextCalled = false;
        var sut = new LicenseMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await InvokeMiddlewareAsync(sut, context, licenseService);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_DeploymentLockdown_AllowsOnlyHealthAndActivation()
    {
        var snapshot = new LicenseStatusResponse(false, false, true, 0, Now.AddDays(-70), "machine");
        var licenseService = CreateLicenseService(snapshot);
        var blockedContext = CreateContext("/api/auth/login", HttpMethods.Post);
        var sut = new LicenseMiddleware(_ => Task.CompletedTask);

        await InvokeMiddlewareAsync(sut, blockedContext, licenseService);

        var blockedBody = await ReadBodyAsync(blockedContext);
        Assert.Equal(StatusCodes.Status403Forbidden, blockedContext.Response.StatusCode);
        Assert.Contains("DEPLOYMENT_LICENSE_LOCKDOWN", blockedBody, StringComparison.Ordinal);

        var allowedContext = CreateContext("/api/license/activate", HttpMethods.Post);
        var nextCalled = false;
        var allowedSut = new LicenseMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await InvokeMiddlewareAsync(allowedSut, allowedContext, licenseService);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_Development_SkipsAllLicenseEnforcement()
    {
        var snapshot = new LicenseStatusResponse(false, false, true, 0, Now.AddDays(-70), "machine");
        var tenantStatus = new LicenseStatusInfo { CanAccess = false };
        var licenseService = CreateLicenseService(snapshot, tenantStatus);
        var context = CreateContext("/api/admin/products", HttpMethods.Post);
        var nextCalled = false;
        var sut = new LicenseMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await InvokeMiddlewareAsync(sut, context, licenseService, isDevelopment: true);

        Assert.True(nextCalled);
        licenseService.Verify(
            x => x.ValidateAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_TenantLicenseLocked_BlocksPosWithLicenseLockedCode()
    {
        var snapshot = new LicenseStatusResponse(true, false, false, 90, Now.AddDays(90), "machine");
        var tenantStatus = new LicenseStatusInfo
        {
            CanAccess = false,
            IsLocked = true,
            RequiresRenewal = true,
            ValidUntil = Now.AddDays(-30),
            DaysOverdue = 30,
            StatusMessage = "Lizenz abgelaufen! POS ist gesperrt. Nur Super-Administrator kann entsperren.",
            StatusMessageKey = "license.status.locked",
        };
        var licenseService = CreateLicenseService(snapshot, tenantStatus);
        var context = CreateContext("/api/pos/cart", HttpMethods.Post);
        var nextCalled = false;
        var sut = new LicenseMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await InvokeMiddlewareAsync(sut, context, licenseService);

        var body = await ReadBodyAsync(context);
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Contains(LicenseMiddleware.LicenseLockedCode, body, StringComparison.Ordinal);
        Assert.Contains("\"success\":false", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("POS ist gesperrt", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_TenantLicenseLocked_AllowsAdminRenewalPaths()
    {
        var snapshot = new LicenseStatusResponse(true, false, false, 90, Now.AddDays(90), "machine");
        var tenantStatus = new LicenseStatusInfo
        {
            CanAccess = false,
            IsLocked = true,
            RequiresRenewal = true,
            ValidUntil = Now.AddDays(-30),
            DaysOverdue = 30,
            StatusMessage = "Lizenz abgelaufen! POS ist gesperrt.",
        };
        var licenseService = CreateLicenseService(snapshot, tenantStatus);
        var context = CreateContext("/api/admin/products", HttpMethods.Get);
        var nextCalled = false;
        var sut = new LicenseMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await InvokeMiddlewareAsync(sut, context, licenseService);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_TenantInGracePeriod_AddsLicenseHeaders()
    {
        var graceRemaining = Math.Max(0, LicenseGracePeriodConfig.GracePeriodDays - 5);
        var snapshot = new LicenseStatusResponse(true, false, false, 90, Now.AddDays(90), "machine");
        var tenantStatus = new LicenseStatusInfo
        {
            CanAccess = true,
            CanTransact = true,
            DaysRemaining = -5,
            DaysOverdue = 5,
            GracePeriodRemaining = graceRemaining,
            IsInGracePeriod = true,
            StatusMessage = $"Mandantenlizenz seit 5 Tag(en) abgelaufen. POS kann noch {graceRemaining} Tag(e) genutzt werden.",
        };
        var licenseService = CreateLicenseService(snapshot, tenantStatus);
        var context = CreateContext("/api/pos/cart", HttpMethods.Post);
        var nextCalled = false;
        var sut = new LicenseMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await InvokeMiddlewareAsync(sut, context, licenseService);

        Assert.True(nextCalled);
        Assert.Equal(tenantStatus.StatusMessage, context.Response.Headers[LicenseMiddleware.LicenseStatusHeaderName].ToString());
        Assert.Equal("true", context.Response.Headers[LicenseMiddleware.LicenseGraceHeaderName].ToString());
        Assert.Equal(graceRemaining.ToString(), context.Response.Headers[LicenseMiddleware.LicenseDaysRemainingHeaderName].ToString());
        Assert.Equal(graceRemaining.ToString(), context.Response.Headers[LicenseMiddleware.LicenseGraceRemainingHeaderName].ToString());
    }

    [Fact]
    public async Task InvokeAsync_PublicEndpoint_SkipsTenantLicenseCheck()
    {
        var snapshot = new LicenseStatusResponse(true, false, false, 90, Now.AddDays(90), "machine");
        var tenantStatus = new LicenseStatusInfo { CanAccess = false };
        var licenseService = CreateLicenseService(snapshot, tenantStatus);
        var context = CreateContext("/api/health", HttpMethods.Get);
        var nextCalled = false;
        var sut = new LicenseMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await InvokeMiddlewareAsync(sut, context, licenseService);

        Assert.True(nextCalled);
        licenseService.Verify(
            x => x.GetLicenseStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
