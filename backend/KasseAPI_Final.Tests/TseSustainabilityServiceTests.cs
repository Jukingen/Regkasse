using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseSustainabilityServiceTests
{
    [Fact]
    public async Task GetSustainabilityReportAsync_AggregatesEnergyAndCarbon()
    {
        await using var db = CreateDb();
        var (tenantId, registerId) = await SeedTenantWithPrimaryAsync(db);
        var now = DateTime.UtcNow;

        db.Receipts.AddRange(
            Receipt(tenantId, registerId, now.AddDays(-2), "sig-a"),
            Receipt(tenantId, registerId, now.AddDays(-1), "sig-b"),
            Receipt(tenantId, registerId, now.AddHours(-3), ""));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var report = await svc.GetSustainabilityReportAsync(tenantId, now.AddDays(-30), now);

        Assert.Equal(tenantId, report.TenantId);
        Assert.True(report.DiagnosticOnly);
        Assert.Equal(3, report.TotalTransactions);
        Assert.Equal(2, report.SignedTransactions);
        Assert.Equal(1, report.ActiveDeviceCount);
        Assert.True(report.TotalEnergyUsage > 0);
        Assert.True(report.TotalCarbonEmission > 0);
        Assert.NotEmpty(report.CarbonTrend);
    }

    [Fact]
    public async Task CalculateCarbonFootprintAsync_SplitsDeviceAndTransactionCo2()
    {
        await using var db = CreateDb();
        var (tenantId, registerId) = await SeedTenantWithPrimaryAsync(db);
        var now = DateTime.UtcNow;
        db.Receipts.Add(Receipt(tenantId, registerId, now.AddDays(-1), "sig"));
        await db.SaveChangesAsync();

        var svc = CreateService(db, new TseOptions
        {
            SustainabilityKgCo2PerSignedTransaction = 0.001,
            SustainabilityKwhPerCloudDeviceDay = 0.1,
            SustainabilityKgCo2PerKwh = 0.2,
        });

        var footprint = await svc.CalculateCarbonFootprintAsync(tenantId, now.AddDays(-10), now);

        Assert.True(footprint.TransactionApiKgCo2 > 0);
        Assert.True(footprint.DeviceEnergyKgCo2 > 0);
        Assert.Equal(
            Math.Round(footprint.DeviceEnergyKgCo2 + footprint.TransactionApiKgCo2, 4),
            footprint.TotalKgCo2);
    }

    [Fact]
    public async Task GetOptimizationSuggestionsAsync_FlagsExcessBackups()
    {
        await using var db = CreateDb();
        var (tenantId, registerId) = await SeedTenantWithPrimaryAsync(db);
        db.TseDevices.AddRange(
            Backup(tenantId, registerId, "S1"),
            Backup(tenantId, registerId, "S2"));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.GetOptimizationSuggestionsAsync(tenantId);

        Assert.Contains(result.Suggestions, s => s.Code == "retire_idle_backups");
        Assert.True(result.PotentialEnergySavedKwh > 0);
    }

    private static TseSustainabilityService CreateService(AppDbContext db, TseOptions? opts = null) =>
        new(
            db,
            Options.Create(opts ?? new TseOptions
            {
                SustainabilityKgCo2PerSignedTransaction = 0.0008,
                SustainabilityKwhPerCloudDeviceDay = 0.12,
                SustainabilityKwhPerSoftDeviceDay = 0.01,
                SustainabilityKgCo2PerKwh = 0.23,
                SustainabilityEurPerKwh = 0.28m,
                SustainabilityIndustryKgCo2PerTransaction = 0.0025,
                SustainabilityDefaultLookbackDays = 30,
            }).ToMonitor(),
            NullLogger<TseSustainabilityService>.Instance);

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_sust_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<(Guid TenantId, Guid RegisterId)> SeedTenantWithPrimaryAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Green Cafe",
            Slug = "green-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var register = new CashRegister
        {
            TenantId = tenantId,
            RegisterNumber = "KASSE-G1",
            Location = "Haupt",
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();

        db.TseDevices.Add(new TseDevice
        {
            SerialNumber = "GRN-P1",
            DeviceType = "Cloud",
            VendorId = "auto",
            ProductId = "fiskaly",
            Provider = TseOptions.ProviderFiskaly,
            TenantId = tenantId,
            CashRegisterId = register.Id,
            KassenId = register.Id,
            DeviceId = "green-primary",
            IsConnected = true,
            CanCreateInvoices = true,
            IsActive = true,
            IsPrimary = true,
            HealthStatus = TseHealthStatus.Healthy,
            HealthScore = 100,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return (tenantId, register.Id);
    }

    private static TseDevice Backup(Guid tenantId, Guid registerId, string serial) =>
        new()
        {
            SerialNumber = serial,
            DeviceType = "Cloud",
            VendorId = "auto",
            ProductId = "fiskaly",
            Provider = TseOptions.ProviderFiskaly,
            TenantId = tenantId,
            CashRegisterId = registerId,
            KassenId = registerId,
            DeviceId = serial.ToLowerInvariant(),
            IsConnected = true,
            CanCreateInvoices = true,
            IsActive = true,
            IsPrimary = false,
            IsBackup = true,
            HealthStatus = TseHealthStatus.Healthy,
            HealthScore = 100,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        };

    private static Receipt Receipt(Guid tenantId, Guid registerId, DateTime issuedAt, string signature) =>
        new()
        {
            ReceiptId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = registerId,
            ReceiptNumber = Guid.NewGuid().ToString("N")[..12],
            IssuedAt = issuedAt,
            SubTotal = 10m,
            TaxTotal = 2m,
            GrandTotal = 12m,
            SignatureValue = signature,
            CreatedAt = issuedAt,
        };
}
