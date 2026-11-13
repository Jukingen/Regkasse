using System.Security.Claims;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PosTseControllerTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [Fact]
    public async Task GetStatus_WithoutTenant_Returns404()
    {
        var status = new Mock<IPosTseStatusService>();
        var controller = new PosTseController(
            status.Object,
            TenantTestDoubles.TenantAccessorReturning(null),
            NullLogger<PosTseController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, "u1") },
                        "Test")),
                },
            },
        };

        var result = await controller.GetStatus(null, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        status.Verify(
            s => s.GetStatusAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetStatus_WithTenant_ReturnsOkPayload()
    {
        var dto = new PosTseStatusDto
        {
            Status = PosTseIndicatorStatuses.Active,
            Message = "TSE is operational.",
            ScuId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
            TssId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
            OperationalHealth = "Online",
        };
        var status = new Mock<IPosTseStatusService>();
        status.Setup(s => s.GetStatusAsync(TenantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var controller = new PosTseController(
            status.Object,
            TenantTestDoubles.TenantAccessorReturning(TenantId),
            NullLogger<PosTseController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, "u1") },
                        "Test")),
                },
            },
        };

        var result = await controller.GetStatus(null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PosTseStatusDto>(ok.Value);
        Assert.Equal(PosTseIndicatorStatuses.Active, payload.Status);
        Assert.Equal(dto.ScuId, payload.ScuId);
    }
}
