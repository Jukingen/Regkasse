using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseControllerMandantStatusTests
{
    [Fact]
    public async Task GetStatus_WithTenantId_OverlaysMandantGraceFields()
    {
        var tenantId = Guid.NewGuid();
        var licenseMock = new Mock<ILicenseService>();
        licenseMock
            .Setup(x => x.GetCurrentStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicenseStatusResponse(
                IsValid: true,
                IsTrial: false,
                IsExpired: false,
                DaysRemaining: 365,
                ExpiryDate: DateTime.UtcNow.AddDays(365),
                MachineHash: "test",
                EnabledFeatures: LicenseFeatureIds.All));
        licenseMock
            .Setup(x => x.GetLicenseStatusAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicenseStatusInfo
            {
                CanAccess = true,
                CanTransact = true,
                DaysRemaining = 5,
                IsInGracePeriod = false,
                GracePeriodRemaining = 0,
                StatusMessage = "Lizenz läuft in 5 Tagen ab",
                RequiresRenewal = false,
            });

        var controller = CreateController(licenseMock.Object);
        var result = await controller.GetStatus(tenantId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<LicensePublicStatusDto>(ok.Value);
        Assert.True(dto.CanAccess);
        Assert.True(dto.CanTransact);
        Assert.Equal(5, dto.DaysRemaining);
        Assert.Equal("Lizenz läuft in 5 Tagen ab", dto.StatusMessage);
        Assert.False(dto.IsInGracePeriod);
    }

    [Fact]
    public async Task GetStatus_WhenMandantBlocked_ExposesCanAccessFalse()
    {
        var tenantId = Guid.NewGuid();
        var licenseMock = new Mock<ILicenseService>();
        licenseMock
            .Setup(x => x.GetCurrentStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicenseStatusResponse(
                IsValid: true,
                IsTrial: false,
                IsExpired: false,
                DaysRemaining: 30,
                ExpiryDate: DateTime.UtcNow.AddDays(30),
                MachineHash: "test"));
        licenseMock
            .Setup(x => x.GetLicenseStatusAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicenseStatusInfo
            {
                CanAccess = false,
                CanTransact = false,
                RequiresRenewal = true,
                StatusMessage = "Lizenz abgelaufen. Zugriff gesperrt.",
            });

        var controller = CreateController(licenseMock.Object);
        var result = await controller.GetStatus(tenantId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<LicensePublicStatusDto>(ok.Value);
        Assert.False(dto.CanAccess);
        Assert.True(dto.RequiresRenewal);
    }

    private static LicenseController CreateController(ILicenseService licenseService)
    {
        var tenantAccessor = new Mock<ICurrentTenantAccessor>();
        tenantAccessor.Setup(x => x.TenantId).Returns(Guid.Empty);

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LicenseController_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(dbOptions, NullCurrentTenantAccessor.Instance);

        return new LicenseController(
            licenseService,
            Options.Create(new Configuration.LicenseOptions()),
            Mock.Of<IWebHostEnvironment>(),
            NullLogger<LicenseController>.Instance,
            tenantAccessor.Object,
            db);
    }
}
