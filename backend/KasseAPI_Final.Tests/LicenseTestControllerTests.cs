using KasseAPI_Final.Controllers;
using KasseAPI_Final.Services.LicenseTest;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseTestControllerTests
{
    private static LicenseTestController CreateController(bool isDevelopment)
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName)
            .Returns(isDevelopment ? Environments.Development : Environments.Production);

        var service = new Mock<ILicenseTestService>();

        return new LicenseTestController(
            service.Object,
            env.Object,
            NullLogger<LicenseTestController>.Instance);
    }

    [Fact]
    public async Task Update_OutsideDevelopment_ReturnsNotFound()
    {
        var controller = CreateController(isDevelopment: false);

        var result = await controller.Update(
            new LicenseTestRequest
            {
                TenantId = Guid.NewGuid(),
                ValidUntil = DateTime.UtcNow.AddDays(7),
            });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetSnapshot_OutsideDevelopment_ReturnsNotFound()
    {
        var controller = CreateController(isDevelopment: false);

        var result = await controller.GetSnapshot(null);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
