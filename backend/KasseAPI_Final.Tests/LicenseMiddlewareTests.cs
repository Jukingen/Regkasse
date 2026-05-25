using System.Text;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseMiddlewareTests
{
    private static readonly DateTime Now = new(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc);

    private static DefaultHttpContext CreateContext(string path, string method)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static Mock<ILicenseService> CreateLicenseService(LicenseStatusResponse deploymentSnapshot)
    {
        var mock = new Mock<ILicenseService>(MockBehavior.Loose);
        mock.Setup(x => x.ValidateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicenseValidationResult { IsLicenseOperational = true });
        mock.Setup(x => x.GetDeploymentStatus()).Returns(deploymentSnapshot);
        mock.Setup(x => x.GetStatus()).Returns(deploymentSnapshot);
        mock.Setup(x => x.GetCurrentStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(deploymentSnapshot);
        mock.Setup(x => x.GetCurrentDeploymentStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(deploymentSnapshot);
        mock.SetupGet(x => x.IsLicenseSnapshotInitialized).Returns(true);
        return mock;
    }

    private static async Task<string> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task InvokeAsync_DeploymentGraceReadOnly_BlocksWriteRoutes()
    {
        var snapshot = new LicenseStatusResponse(false, false, true, 0, Now.AddDays(-20), "machine");
        var licenseService = CreateLicenseService(snapshot);
        var context = CreateContext("/api/admin/products", HttpMethods.Post);
        var nextCalled = false;
        var sut = new LicenseMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await sut.InvokeAsync(context, licenseService.Object, new DeploymentLicenseValidator());

        var body = await ReadBodyAsync(context);
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Contains("DEPLOYMENT_LICENSE_READ_ONLY", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_DeploymentGraceReadOnly_AllowsAuthWrites()
    {
        var snapshot = new LicenseStatusResponse(false, false, true, 0, Now.AddDays(-20), "machine");
        var licenseService = CreateLicenseService(snapshot);
        var context = CreateContext("/api/auth/login", HttpMethods.Post);
        var nextCalled = false;
        var sut = new LicenseMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await sut.InvokeAsync(context, licenseService.Object, new DeploymentLicenseValidator());

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_DeploymentLockdown_AllowsOnlyHealthAndActivation()
    {
        var snapshot = new LicenseStatusResponse(false, false, true, 0, Now.AddDays(-70), "machine");
        var licenseService = CreateLicenseService(snapshot);
        var blockedContext = CreateContext("/api/auth/login", HttpMethods.Post);
        var sut = new LicenseMiddleware(_ => Task.CompletedTask);

        await sut.InvokeAsync(blockedContext, licenseService.Object, new DeploymentLicenseValidator());

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

        await allowedSut.InvokeAsync(allowedContext, licenseService.Object, new DeploymentLicenseValidator());

        Assert.True(nextCalled);
    }
}
