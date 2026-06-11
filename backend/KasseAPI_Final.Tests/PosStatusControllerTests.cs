using System.Security.Claims;
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

public class PosStatusControllerTests
{
    private static readonly Guid TenantId = LegacyDefaultTenantIds.Primary;

    [Fact]
    public async Task GetOverview_WithoutTenant_Returns404()
    {
        var status = new Mock<IPosStatusService>();
        var controller = new PosStatusController(
            status.Object,
            TenantTestDoubles.TenantAccessorReturning(null),
            NullLogger<PosStatusController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, "u1") },
                    "Test")),
            },
        };

        var result = await controller.GetOverview(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetOverview_WithTenant_ReturnsOkPayload()
    {
        var overview = new PosStatusOverviewDto
        {
            ServerTimeUtc = DateTime.UtcNow,
            License = new LicensePublicStatusDto { LicenseType = "Licensed", IsValid = true },
        };

        var status = new Mock<IPosStatusService>();
        status.Setup(s => s.GetOverviewAsync(
                "u1",
                It.IsAny<ClaimsPrincipal>(),
                TenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(overview);

        var controller = new PosStatusController(
            status.Object,
            TenantTestDoubles.TenantAccessorReturning(TenantId),
            NullLogger<PosStatusController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, "u1") },
                    "Test")),
            },
        };

        var result = await controller.GetOverview(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<PosStatusOverviewDto>(ok.Value);
        Assert.Equal("Licensed", dto.License.LicenseType);
    }
}
