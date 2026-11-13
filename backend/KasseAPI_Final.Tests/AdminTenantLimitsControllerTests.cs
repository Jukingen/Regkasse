using System.Reflection;
using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Limits;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminTenantLimitsControllerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static AdminTenantLimitsController CreateController(ITenantLimitService service)
    {
        var controller = new AdminTenantLimitsController(
            service,
            NullLogger<AdminTenantLimitsController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "root"),
                        new Claim(ClaimTypes.Role, Roles.SuperAdmin),
                    },
                    "Test")),
            },
        };
        return controller;
    }

    [Fact]
    public void Controller_RequiresSuperAdminRole()
    {
        var attr = typeof(AdminTenantLimitsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(Roles.SuperAdmin, attr.Roles);
    }

    [Fact]
    public async Task GetLimits_ReturnsDto()
    {
        var row = TenantLimits.CreateDefault(TenantId);
        var service = new Mock<ITenantLimitService>();
        service.Setup(s => s.GetLimitsAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(row);

        var result = await CreateController(service.Object).GetLimits(TenantId);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TenantLimitsDto>(ok.Value);
        Assert.Equal(TenantId, dto.TenantId);
        Assert.Equal(TenantLimits.DefaultMaxActiveRegistersPerUser, dto.MaxActiveRegistersPerUser);
    }

    [Fact]
    public async Task GetLimits_UnknownTenant_Returns404()
    {
        var service = new Mock<ITenantLimitService>();
        service
            .Setup(s => s.GetLimitsAsync(TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Tenant not found."));

        var result = await CreateController(service.Object).GetLimits(TenantId);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateLimits_PersistsViaService()
    {
        var request = new UpdateTenantLimitsRequest { MaxActiveRegistersPerUser = 4 };
        var updated = TenantLimits.CreateDefault(TenantId);
        updated.MaxActiveRegistersPerUser = 4;
        var service = new Mock<ITenantLimitService>();
        service
            .Setup(s => s.UpdateLimitsAsync(TenantId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        var result = await CreateController(service.Object).UpdateLimits(TenantId, request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TenantLimitsDto>(ok.Value);
        Assert.Equal(4, dto.MaxActiveRegistersPerUser);
    }

    [Fact]
    public async Task ResetLimits_ReturnsDefaults()
    {
        var service = new Mock<ITenantLimitService>();
        service.Setup(s => s.ResetLimitsAsync(TenantId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        service
            .Setup(s => s.GetLimitsAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TenantLimits.CreateDefault(TenantId));

        var result = await CreateController(service.Object).ResetLimits(TenantId);

        Assert.IsType<OkObjectResult>(result);
        service.Verify(s => s.ResetLimitsAsync(TenantId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
