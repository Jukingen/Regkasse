using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminSessionControllerTests
{
    [Fact]
    public async Task GetActiveSessions_ReturnsOk()
    {
        var sessions = new List<AdminActiveSessionDto>
        {
            new() { Id = Guid.NewGuid(), UserId = "u1", UserName = "cashier1", ClientApp = "admin", IsActive = true },
        };
        var service = new Mock<ISessionManagementService>();
        service.Setup(s => s.GetActiveSessionsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(sessions);

        var result = await CreateController(service.Object).GetActiveSessions(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(sessions, ok.Value);
    }

    [Fact]
    public async Task TerminateSession_NotFound_WhenMissing()
    {
        var service = new Mock<ISessionManagementService>();
        service
            .Setup(s => s.TerminateSessionAsync(It.IsAny<Guid>(), "sa-1", Roles.SuperAdmin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateController(service.Object).TerminateSession(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task ForceLogout_ReturnsSuccess()
    {
        var service = new Mock<ISessionManagementService>();
        service
            .Setup(s => s.ForceLogoutAsync("u1", "sa-1", Roles.SuperAdmin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateController(service.Object).ForceLogout("u1", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ForceLogoutResultDto>(ok.Value);
        Assert.True(body.Success);
    }

    [Fact]
    public async Task TerminateAllSessions_PassesCurrentSidAsException()
    {
        var currentSid = Guid.NewGuid();
        var service = new Mock<ISessionManagementService>();
        service
            .Setup(s => s.TerminateAllSessionsAsync("sa-1", Roles.SuperAdmin, currentSid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var controller = CreateController(service.Object, currentSid);
        var result = await controller.TerminateAllSessions(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<TerminateSessionsCountDto>(ok.Value);
        Assert.Equal(3, body.TerminatedCount);
    }

    private static AdminSessionController CreateController(
        ISessionManagementService service,
        Guid? currentSessionId = null)
    {
        var controller = new AdminSessionController(service, NullLogger<AdminSessionController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        var claims = new List<Claim>
        {
            new("userId", "sa-1"),
            new("role", Roles.SuperAdmin),
            new(PermissionCatalog.PermissionClaimType, AppPermissions.SystemCritical),
        };
        if (currentSessionId.HasValue)
            claims.Add(new Claim("sid", currentSessionId.Value.ToString("D")));

        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        return controller;
    }
}
