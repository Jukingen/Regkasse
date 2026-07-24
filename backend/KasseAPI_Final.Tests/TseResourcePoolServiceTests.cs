using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseResourcePoolServiceTests
{
    [Fact]
    public async Task CreateAndAssign_UpdatesCapacity()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db, "Pool Cafe", "pool-cafe");
        var svc = CreateService(db);

        var pool = await svc.CreateResourcePoolAsync(new CreateTseResourcePoolRequestDto
        {
            Name = "Shared-EU-1",
            Type = TseResourcePoolTypes.Shared,
            TotalCapacity = 5,
        });

        Assert.Equal(5, pool.TotalCapacity);
        Assert.Equal(0, pool.UsedCapacity);
        Assert.Equal(5, pool.AvailableCapacity);

        var assign = await svc.AssignTenantToPoolAsync(tenantId, pool.Id, reservedCapacity: 2);
        Assert.True(assign.Success);
        Assert.NotNull(assign.Pool);
        Assert.Equal(2, assign.Pool!.UsedCapacity);
        Assert.Equal(3, assign.Pool.AvailableCapacity);
        Assert.Contains(tenantId, assign.Pool.AssignedTenants);
    }

    [Fact]
    public async Task DedicatedPool_RejectsSecondTenant()
    {
        await using var db = CreateDb();
        var t1 = await SeedTenantAsync(db, "A", "a");
        var t2 = await SeedTenantAsync(db, "B", "b");
        var svc = CreateService(db);

        var pool = await svc.CreateResourcePoolAsync(new CreateTseResourcePoolRequestDto
        {
            Name = "Dedicated-1",
            Type = TseResourcePoolTypes.Dedicated,
            TotalCapacity = 10,
        });

        var first = await svc.AssignTenantToPoolAsync(t1, pool.Id);
        Assert.True(first.Success);

        var second = await svc.AssignTenantToPoolAsync(t2, pool.Id);
        Assert.False(second.Success);
        Assert.Contains("limit", second.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetPoolMetrics_CountsDevicesAndSignedReceipts()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db, "Metrics Cafe", "metrics-cafe");
        var registerId = Guid.NewGuid();
        db.TseDevices.Add(new TseDevice
        {
            SerialNumber = "POOL-D1",
            DeviceType = "Cloud",
            VendorId = "auto",
            ProductId = "fiskaly",
            Provider = TseOptions.ProviderFiskaly,
            TenantId = tenantId,
            CashRegisterId = registerId,
            KassenId = registerId,
            DeviceId = "pool-device",
            IsConnected = true,
            CanCreateInvoices = true,
            IsActive = true,
            IsPrimary = true,
            HealthStatus = TseHealthStatus.Healthy,
            HealthScore = 95,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        });
        db.Receipts.Add(new Receipt
        {
            ReceiptId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = registerId,
            ReceiptNumber = "R1",
            IssuedAt = DateTime.UtcNow.AddDays(-1),
            SubTotal = 10,
            TaxTotal = 2,
            GrandTotal = 12,
            SignatureValue = "sig",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var pool = await svc.CreateResourcePoolAsync(new CreateTseResourcePoolRequestDto
        {
            Name = "Metrics-Pool",
            Type = TseResourcePoolTypes.Shared,
            TotalCapacity = 3,
        });
        await svc.AssignTenantToPoolAsync(tenantId, pool.Id);

        var metrics = await svc.GetPoolMetricsAsync(pool.Id);
        Assert.Equal(1, metrics.AssignedTenantCount);
        Assert.Equal(1, metrics.ActiveDeviceCount);
        Assert.Equal(1, metrics.HealthyDeviceCount);
        Assert.Equal(1, metrics.SignedTransactionsLast30Days);
        Assert.True(metrics.AverageHealthScore >= 90);
    }

    private static TseResourcePoolService CreateService(AppDbContext db) =>
        new(db, NullLogger<TseResourcePoolService>.Instance);

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_pool_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<Guid> SeedTenantAsync(AppDbContext db, string name, string slug)
    {
        var id = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = id,
            Name = name,
            Slug = slug,
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return id;
    }
}
