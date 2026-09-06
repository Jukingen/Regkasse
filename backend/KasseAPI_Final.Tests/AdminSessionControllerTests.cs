using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
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
        service
            .Setup(s => s.ListSessionsAsync(
                It.IsAny<SessionManagementAccess>(),
                It.IsAny<AdminSessionListFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var result = await CreateController(service.Object).GetActiveSessions(
            search: null,
            tenantId: null,
            role: null,
            status: null,
            userId: null,
            CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(sessions, ok.Value);
    }

    [Fact]
    public async Task GetActiveSessions_ManagerWithoutTenant_ReturnsNotFound()
    {
        var service = new Mock<ISessionManagementService>();
        var result = await CreateController(service.Object, role: Roles.Manager, ambientTenantId: null)
            .GetActiveSessions(null, null, null, null, null, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result.Result);
        service.Verify(
            s => s.ListSessionsAsync(
                It.IsAny<SessionManagementAccess>(),
                It.IsAny<AdminSessionListFilter>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetActiveSessions_Manager_ForcesAmbientTenantFilter()
    {
        var tenantId = Guid.NewGuid();
        SessionManagementAccess? capturedAccess = null;
        AdminSessionListFilter? capturedFilter = null;
        var service = new Mock<ISessionManagementService>();
        service
            .Setup(s => s.ListSessionsAsync(
                It.IsAny<SessionManagementAccess>(),
                It.IsAny<AdminSessionListFilter>(),
                It.IsAny<CancellationToken>()))
            .Callback<SessionManagementAccess, AdminSessionListFilter, CancellationToken>((a, f, _) =>
            {
                capturedAccess = a;
                capturedFilter = f;
            })
            .ReturnsAsync(Array.Empty<AdminActiveSessionDto>());

        var result = await CreateController(
                service.Object,
                role: Roles.Manager,
                ambientTenantId: tenantId)
            .GetActiveSessions(
                search: "anna",
                tenantId: Guid.NewGuid(),
                role: Roles.Cashier,
                status: "all",
                userId: null,
                CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(capturedAccess);
        Assert.False(capturedAccess!.IsSuperAdmin);
        Assert.Equal(tenantId, capturedAccess.ScopedTenantId);
        Assert.Equal(tenantId, capturedFilter!.TenantId);
        Assert.Equal("anna", capturedFilter.Search);
        Assert.Equal(Roles.Cashier, capturedFilter.Role);
        Assert.Equal("all", capturedFilter.Status);
    }

    [Fact]
    public async Task TerminateSession_NotFound_WhenMissing()
    {
        var service = new Mock<ISessionManagementService>();
        service
            .Setup(s => s.TerminateSessionAsync(
                It.IsAny<Guid>(),
                "sa-1",
                Roles.SuperAdmin,
                It.IsAny<CancellationToken>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync(AdminSessionTerminateResult.NotFound());

        var result = await CreateController(service.Object).TerminateSession(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task LogoutSession_CurrentSession_ReturnsBadRequest()
    {
        var currentSid = Guid.NewGuid();
        var service = new Mock<ISessionManagementService>();
        service
            .Setup(s => s.TerminateSessionAsync(
                currentSid,
                "sa-1",
                Roles.SuperAdmin,
                It.IsAny<CancellationToken>(),
                currentSid,
                null))
            .ReturnsAsync(AdminSessionTerminateResult.CurrentSession());

        var result = await CreateController(service.Object, currentSid).LogoutSession(currentSid, CancellationToken.None);
        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("CANNOT_TERMINATE_CURRENT_SESSION", bad.Value?.ToString() ?? string.Empty, StringComparison.Ordinal);
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
            .Setup(s => s.TerminateAllSessionsAsync(
                "sa-1",
                Roles.SuperAdmin,
                currentSid,
                It.IsAny<CancellationToken>(),
                null))
            .ReturnsAsync(3);

        var controller = CreateController(service.Object, currentSid);
        var result = await controller.TerminateAllSessions(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<TerminateSessionsCountDto>(ok.Value);
        Assert.Equal(3, body.TerminatedCount);
    }

    [Fact]
    public async Task LogoutBulk_PassesIdsAndSkipsCurrent()
    {
        var currentSid = Guid.NewGuid();
        var other = Guid.NewGuid();
        var service = new Mock<ISessionManagementService>();
        service
            .Setup(s => s.TerminateBulkAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.Contains(other)),
                "sa-1",
                Roles.SuperAdmin,
                currentSid,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await CreateController(service.Object, currentSid).LogoutBulkSessions(
            new LogoutBulkSessionsRequestDto { SessionIds = [other, currentSid] },
            CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<TerminateSessionsCountDto>(ok.Value);
        Assert.Equal(1, body.TerminatedCount);
    }

    private static AdminSessionController CreateController(
        ISessionManagementService service,
        Guid? currentSessionId = null,
        string role = Roles.SuperAdmin,
        Guid? ambientTenantId = null)
    {
        var tenant = new Mock<ICurrentTenantAccessor>();
        tenant.SetupProperty(t => t.TenantId, ambientTenantId);

        var controller = new AdminSessionController(
            service,
            tenant.Object,
            NullLogger<AdminSessionController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        var claims = new List<Claim>
        {
            new("userId", "sa-1"),
            new("role", role),
            new(PermissionCatalog.PermissionClaimType, AppPermissions.SystemCritical),
            new(PermissionCatalog.PermissionClaimType, AppPermissions.UserView),
            new(PermissionCatalog.PermissionClaimType, AppPermissions.UserManage),
        };
        if (currentSessionId.HasValue)
            claims.Add(new Claim("sid", currentSessionId.Value.ToString("D")));

        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        return controller;
    }
}
