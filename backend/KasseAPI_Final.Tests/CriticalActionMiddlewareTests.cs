using System.Security.Claims;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.CriticalActions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CriticalActionMiddlewareTests
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
        var middleware = CreateMiddleware(enabled: false, bypassInDev: false, Environments.Production, next);
        var context = CreateContext("POST", "/api/admin/products/deactivate-all");
        var approvals = new Mock<ICriticalActionApprovalService>();

        await middleware.InvokeAsync(context, approvals.Object);

        Assert.True(nextCalled);
        approvals.Verify(a => a.MatchCriticalAction(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Invoke_WhenEnabled_MissingHeader_Returns403()
    {
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var middleware = CreateMiddleware(enabled: true, bypassInDev: false, Environments.Production, next);
        var context = CreateContext("POST", "/api/admin/products/deactivate-all", authenticated: true);
        var approvals = new Mock<ICriticalActionApprovalService>();
        approvals.Setup(a => a.MatchCriticalAction("POST", "/api/admin/products/deactivate-all"))
            .Returns(CriticalActionType.DeleteAllProducts);

        await middleware.InvokeAsync(context, approvals.Object);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_WhenEnabled_ValidHeader_PassesThrough()
    {
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var middleware = CreateMiddleware(enabled: true, bypassInDev: false, Environments.Production, next);
        var context = CreateContext("POST", "/api/admin/products/deactivate-all", authenticated: true);
        context.Request.Headers[CriticalActionOptions.ApprovalHeaderName] = "valid-token";

        var approvals = new Mock<ICriticalActionApprovalService>();
        approvals.Setup(a => a.MatchCriticalAction("POST", "/api/admin/products/deactivate-all"))
            .Returns(CriticalActionType.DeleteAllProducts);
        approvals.Setup(a => a.VerifyApprovalAsync(
                "user-1",
                "valid-token",
                "/api/admin/products/deactivate-all",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await middleware.InvokeAsync(context, approvals.Object);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Invoke_WhenDevelopmentBypass_PassesThrough()
    {
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var middleware = CreateMiddleware(enabled: true, bypassInDev: true, Environments.Development, next);
        var context = CreateContext("POST", "/api/admin/products/deactivate-all");
        var approvals = new Mock<ICriticalActionApprovalService>(MockBehavior.Strict);

        await middleware.InvokeAsync(context, approvals.Object);

        Assert.True(nextCalled);
    }

    private static CriticalActionMiddleware CreateMiddleware(
        bool enabled,
        bool bypassInDev,
        string environmentName,
        RequestDelegate next)
    {
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(environmentName);
        var options = new CriticalActionOptions
        {
            Enabled = enabled,
            BypassInDevelopment = bypassInDev,
        };
        var monitor = new Mock<IOptionsMonitor<CriticalActionOptions>>();
        monitor.SetupGet(m => m.CurrentValue).Returns(options);

        return new CriticalActionMiddleware(
            next,
            NullLogger<CriticalActionMiddleware>.Instance,
            env.Object,
            monitor.Object);
    }

    private static DefaultHttpContext CreateContext(string method, string path, bool authenticated = false)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (authenticated)
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "user-1"), new Claim("userId", "user-1")],
                "Test");
            context.User = new ClaimsPrincipal(identity);
        }

        return context;
    }
}
