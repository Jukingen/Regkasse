using System.Security.Claims;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
namespace KasseAPI_Final.Tests;

/// <summary>
/// Unit tests for GET /api/AuditLog/user/{userId} – pagination, empty list, and response shape.
/// </summary>
public class AuditLogControllerGetUserAuditLogsTests
{
    private static AuditLogController CreateController(
        IAuditLogService auditLogService,
        string? userId = "actor-1",
        string role = "Administrator")
    {
        var logger = new Mock<ILogger<AuditLogController>>().Object;
        var controller = new AuditLogController(auditLogService, logger);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId ?? ""),
            new(ClaimTypes.Role, role),
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };
        return controller;
    }

    [Fact]
    public async Task GetUserAuditLogs_WhenUserIdEmpty_ReturnsBadRequest()
    {
        var mock = new Mock<IAuditLogService>();
        var controller = CreateController(mock.Object);

        var result = await controller.GetUserAuditLogs("", page: 1, pageSize: 10);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task GetUserAuditLogs_WhenUserIdWhiteSpace_ReturnsBadRequest()
    {
        var mock = new Mock<IAuditLogService>();
        var controller = CreateController(mock.Object);

        var result = await controller.GetUserAuditLogs("   ", page: 1, pageSize: 10);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetUserAuditLogs_WhenNoLogs_Returns200WithEmptyList()
    {
        var mock = new Mock<IAuditLogService>();
        mock.Setup(x => x.GetUserAuditLogsAsync(It.IsAny<string>(), null, null, 1, 10))
            .ReturnsAsync(Array.Empty<AuditLog>());
        mock.Setup(x => x.GetUserLifecycleAuditLogsCountAsync(It.IsAny<string>(), null, null))
            .ReturnsAsync(0);
        var controller = CreateController(mock.Object);

        var result = await controller.GetUserAuditLogs("user-1", page: 1, pageSize: 10);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuditLogsResponse>(ok.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.AuditLogs);
        Assert.Empty(response.AuditLogs);
        Assert.Equal(0, response.TotalCount);
        Assert.Equal(0, response.TotalPages);
        Assert.Equal(1, response.Page);
        Assert.Equal(10, response.PageSize);
    }

    [Fact]
    public async Task GetUserAuditLogs_WhenLogsExist_Returns200WithListAndPagination()
    {
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            SessionId = "s1",
            UserId = "admin-1",
            UserRole = "Administrator",
            Action = AuditLogActions.USER_UPDATE,
            EntityType = AuditLogEntityTypes.USER,
            EntityName = "user-1",
            Status = AuditLogStatus.Success,
            Timestamp = DateTime.UtcNow,
            Description = "User updated",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        var mock = new Mock<IAuditLogService>();
        mock.Setup(x => x.GetUserAuditLogsAsync("user-1", null, null, 1, 10))
            .ReturnsAsync(new[] { log });
        mock.Setup(x => x.GetUserLifecycleAuditLogsCountAsync("user-1", null, null))
            .ReturnsAsync(1);
        var controller = CreateController(mock.Object);

        var result = await controller.GetUserAuditLogs("user-1", page: 1, pageSize: 10);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuditLogsResponse>(ok.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.AuditLogs);
        Assert.Single(response.AuditLogs);
        Assert.Equal(AuditLogActions.USER_UPDATE, response.AuditLogs[0].Action);
        Assert.Equal("user-1", response.AuditLogs[0].EntityName);
        Assert.Equal(1, response.TotalCount);
        Assert.Equal(1, response.TotalPages);
        Assert.Equal(1, response.Page);
        Assert.Equal(10, response.PageSize);
    }
}
