using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Moq;

namespace KasseAPI_Final.Tests;

internal static class TseProvisioningTestDoubles
{
    /// <summary>Returns success for cash-register / tenant provisioning without writing TSE rows.</summary>
    public static ITseProvisioningService Successful(Mock<ITseProvisioningService>? capture = null)
    {
        var mock = capture ?? new Mock<ITseProvisioningService>();

        mock.Setup(x => x.ProvisionTseForCashRegisterAsync(
                It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid cashRegisterId, bool _, CancellationToken __) =>
            {
                var device = new TseDevice
                {
                    Id = Guid.NewGuid(),
                    KassenId = cashRegisterId,
                    SerialNumber = $"TEST-{cashRegisterId:N}"[..Math.Min(100, 5 + 32)],
                    DeviceType = "Fake",
                    VendorId = "VID_TEST",
                    ProductId = "PID_TEST",
                    IsConnected = true,
                    CanCreateInvoices = true,
                    CertificateStatus = "VALID",
                    MemoryStatus = "OK",
                    FinanzOnlineUsername = string.Empty,
                    IsActive = true,
                };
                return TseProvisioningResult.Success(device, signatureChainInitialized: true);
            });

        mock.Setup(x => x.ProvisionTseForTenantAsync(
                It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid tenantId, bool _, CancellationToken __) =>
                TseProvisioningResult.Success(
                    new TseDevice
                    {
                        Id = Guid.NewGuid(),
                        SerialNumber = $"TENANT-{tenantId:N}"[..Math.Min(100, 7 + 32)],
                        DeviceType = "Fake",
                        VendorId = "VID_TEST",
                        ProductId = "PID_TEST",
                        CertificateStatus = "VALID",
                        MemoryStatus = "OK",
                        FinanzOnlineUsername = string.Empty,
                        IsActive = true,
                    },
                    signatureChainInitialized: true));

        mock.Setup(x => x.GetTseStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid tenantId, CancellationToken _) => new TseProvisioningStatus
            {
                TenantId = tenantId,
                Status = "Operational",
                IsOperational = true,
                TseMode = "Demo",
                SigningMode = "Fake",
            });

        mock.Setup(x => x.PerformHealthCheckAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid tenantId, CancellationToken _) => new TseProvisioningHealthCheck
            {
                TenantId = tenantId,
                IsHealthy = true,
                CheckedAtUtc = DateTime.UtcNow,
                Status = "Healthy",
                ProviderReady = true,
            });

        mock.Setup(x => x.ListDevicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TseDeviceFleetItemDto>());

        mock.Setup(x => x.GetFleetOverviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TseFleetOverviewDto
            {
                TotalDevices = 0,
                ProcessHealthScore = 100,
                ProcessHealthStatus = "Online",
            });

        mock.Setup(x => x.RevokeTseDeviceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TseProvisioningResult.Success(
                new TseDevice
                {
                    Id = Guid.NewGuid(),
                    SerialNumber = "REVOKED",
                    DeviceType = "Fake",
                    VendorId = "VID_TEST",
                    ProductId = "PID_TEST",
                    CertificateStatus = "VALID",
                    MemoryStatus = "OK",
                    FinanzOnlineUsername = string.Empty,
                    IsActive = false,
                },
                signatureChainInitialized: true,
                detail: "revoked"));

        return mock.Object;
    }

    public static ITseProvisioningService Skipped()
    {
        var mock = new Mock<ITseProvisioningService>();
        mock.Setup(x => x.ProvisionTseForCashRegisterAsync(
                It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TseProvisioningResult.Skipped("TseMode=Off; TSE provisioning skipped."));
        mock.Setup(x => x.ProvisionTseForTenantAsync(
                It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TseProvisioningResult.Skipped("TseMode=Off; TSE provisioning skipped."));
        return mock.Object;
    }
}
