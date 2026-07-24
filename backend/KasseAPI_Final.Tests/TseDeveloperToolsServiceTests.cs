using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseDeveloperToolsServiceTests
{
    [Fact]
    public async Task RunDiagnosticsAsync_ReportsDevicesAndConfig()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantWithDeviceAsync(db, TseHealthStatus.Healthy, 95);
        var svc = CreateService(db, isDevelopment: true);

        var result = await svc.RunDiagnosticsAsync(tenantId);

        Assert.Equal("Diagnostics", result.Operation);
        Assert.True(result.Success);
        Assert.Contains(result.Results, r => r.Name == "Devices" && r.IsSuccess);
        Assert.Contains(result.Results, r => r.Name == "TseOptions");
    }

    [Fact]
    public async Task SimulateTrafficAsync_InsertsHealthSamples_NotPayments()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantWithDeviceAsync(db, TseHealthStatus.Healthy, 90);
        var svc = CreateService(db, isDevelopment: true);

        var result = await svc.SimulateTrafficAsync(tenantId, 25, "dev-user");

        Assert.True(result.Success);
        Assert.Equal(25, await db.TseDeviceHealthSamples.CountAsync());
        Assert.Equal(0, await db.PaymentDetails.CountAsync());
        Assert.Contains(result.Results, r => r.Name == "FiscalGuard");
    }

    [Fact]
    public async Task SimulateTrafficAsync_ClampsToMax1000()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantWithDeviceAsync(db, TseHealthStatus.Healthy, 90);
        var svc = CreateService(db, isDevelopment: true);

        var result = await svc.SimulateTrafficAsync(tenantId, 5000);

        Assert.True(result.Success);
        Assert.Equal(1000, await db.TseDeviceHealthSamples.CountAsync());
        Assert.Equal("1000", result.Metadata!["insertedCount"]);
    }

    [Fact]
    public async Task ValidateConfigAsync_FlagsUnknownMode()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantWithDeviceAsync(db, TseHealthStatus.Healthy, 90);
        var svc = CreateService(db, isDevelopment: true, new TseOptions { Mode = "Nope", TseMode = "Device" });

        var result = await svc.ValidateConfigAsync(tenantId);

        Assert.False(result.Success);
        Assert.Contains(result.Results, r => r.Name == "Mode" && !r.IsSuccess);
    }

    [Fact]
    public async Task GenerateTestDataAsync_SeedsSamplesAndIncident()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantWithDeviceAsync(db, TseHealthStatus.Healthy, 88);
        var incidents = new Mock<ITseIncidentService>();
        incidents
            .Setup(x => x.CreateIncidentAsync(
                It.IsAny<CreateTseIncidentRequestDto>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateTseIncidentRequestDto req, string? _, CancellationToken _) =>
                new TseIncidentDto
                {
                    Id = Guid.NewGuid(),
                    TenantId = req.TenantId,
                    Title = req.Title,
                    Description = req.Description,
                    Severity = req.Severity,
                    Status = "Open",
                });

        var svc = CreateService(db, isDevelopment: true, incidents: incidents.Object);
        var result = await svc.GenerateTestDataAsync(tenantId, "actor-1");

        Assert.True(result.Success);
        Assert.True(await db.TseDeviceHealthSamples.CountAsync() >= 5);
        incidents.Verify(
            x => x.CreateIncidentAsync(
                It.Is<CreateTseIncidentRequestDto>(r =>
                    r.TenantId == tenantId && r.Title.Contains("DX seed", StringComparison.Ordinal)),
                "actor-1",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunDiagnosticsAsync_ThrowsOutsideDevelopment()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantWithDeviceAsync(db, TseHealthStatus.Healthy, 90);
        var svc = CreateService(db, isDevelopment: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RunDiagnosticsAsync(tenantId));
    }

    private static TseDeveloperToolsService CreateService(
        AppDbContext db,
        bool isDevelopment,
        TseOptions? options = null,
        ITseIncidentService? incidents = null)
    {
        var opts = options ?? new TseOptions { Mode = "Fake", TseMode = "Demo" };
        var monitor = new Mock<IOptionsMonitor<TseOptions>>();
        monitor.SetupGet(m => m.CurrentValue).Returns(opts);

        var audit = new Mock<IAuditLogService>();
        audit
            .Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        return new TseDeveloperToolsService(
            db,
            monitor.Object,
            incidents ?? Mock.Of<ITseIncidentService>(),
            audit.Object,
            new DxFakeHostEnvironment(isDevelopment ? Environments.Development : Environments.Production),
            NullLogger<TseDeveloperToolsService>.Instance);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_dx_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<Guid> SeedTenantWithDeviceAsync(
        AppDbContext db,
        TseHealthStatus status,
        int score)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "DX Tenant",
            Slug = "dx-tenant",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.TseDevices.Add(new TseDevice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SerialNumber = "DX-SN-1",
            DeviceType = "fiskaly",
            Provider = "fiskaly",
            VendorId = "v",
            ProductId = "p",
            IsConnected = true,
            LastConnectionTime = DateTime.UtcNow,
            LastSignatureTime = DateTime.UtcNow,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            CanCreateInvoices = true,
            FinanzOnlineUsername = "fo",
            FinanzOnlineEnabled = false,
            LastFinanzOnlineSync = DateTime.UtcNow,
            KassenId = Guid.NewGuid(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            HealthStatus = status,
            HealthScore = score,
            LastHealthCheck = DateTime.UtcNow,
            IsPrimary = true,
        });
        await db.SaveChangesAsync();
        return tenantId;
    }
}

public sealed class AdminTseDeveloperToolsControllerTests
{
    [Fact]
    public async Task GetAvailability_ReturnsEnabledInDevelopment()
    {
        var tools = new Mock<ITseDeveloperToolsService>();
        tools.SetupGet(t => t.IsEnabled).Returns(true);
        tools
            .Setup(t => t.GetAvailabilityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TseDeveloperToolsAvailabilityDto
            {
                Enabled = true,
                EnvironmentName = "Development",
                Message = "ok",
            });

        var controller = new AdminTseDeveloperToolsController(
            tools.Object,
            new DxFakeHostEnvironment(Environments.Development),
            NullLogger<AdminTseDeveloperToolsController>.Instance);

        var result = await controller.GetAvailability(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TseDeveloperToolsAvailabilityDto>(ok.Value);
        Assert.True(dto.Enabled);
    }

    [Fact]
    public async Task RunDiagnostics_OutsideDevelopment_ReturnsNotFound()
    {
        var controller = new AdminTseDeveloperToolsController(
            Mock.Of<ITseDeveloperToolsService>(),
            new DxFakeHostEnvironment(Environments.Production),
            NullLogger<AdminTseDeveloperToolsController>.Instance);

        var result = await controller.RunDiagnostics(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<NotFoundResult>(result.Result);
    }
}

file sealed class DxFakeHostEnvironment : IHostEnvironment
{
    public DxFakeHostEnvironment(string name) => EnvironmentName = name;
    public string EnvironmentName { get; set; }
    public string ApplicationName { get; set; } = "Tests";
    public string ContentRootPath { get; set; } = ".";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
