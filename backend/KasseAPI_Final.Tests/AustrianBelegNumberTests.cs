using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Offline;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AustrianBelegNumberTests
{
    private static readonly DateTime BusinessDay = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Format_SameRegisterNumber_DiffersByTenant()
    {
        var dev = Guid.Parse("b0000001-0001-4001-8001-000000000001");
        var lodging = Guid.Parse("b0000002-0002-4002-8002-000000000002");

        var first = AustrianBelegNumber.Format("KASSE-001", dev, BusinessDay, 1);
        var second = AustrianBelegNumber.Format("KASSE-001", lodging, BusinessDay, 1);

        Assert.NotEqual(first, second);
        Assert.Equal($"AT-KASSE-001-{dev:N}-20261101-1", first);
        Assert.Equal($"AT-KASSE-001-{lodging:N}-20261101-1", second);
        Assert.True(first.Length <= Invoice.InvoiceNumberMaxLength);
        Assert.True(second.Length <= Invoice.InvoiceNumberMaxLength);
    }

    [Fact]
    public void Format_MaxRegisterAndSequence_FitsInvoiceColumn()
    {
        var number = AustrianBelegNumber.Format(
            new string('R', 20),
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            BusinessDay,
            int.MaxValue);

        Assert.True(number.Length <= Invoice.InvoiceNumberMaxLength);
    }

    [Fact]
    public void CashRegisterNumbers_DevKeepsKasse001_OtherSlugsDoNotCollide()
    {
        var dev = CashRegisterNumbers.ForTenant(DemoTenantIds.Dev, "dev");
        var lodging = CashRegisterNumbers.ForTenant(Guid.NewGuid(), "dev-beherbergung");
        var retail = CashRegisterNumbers.ForTenant(Guid.NewGuid(), "dev-retail");

        Assert.Equal("KASSE-001", dev);
        Assert.Equal("K-DEV-BEHERBERGUNG", lodging);
        Assert.Equal("K-DEV-RETAIL", retail);
        Assert.NotEqual(dev, lodging);
        Assert.NotEqual(lodging, retail);
        Assert.True(lodging.Length <= CashRegisterNumbers.MaxLength);
    }

    [Fact]
    public async Task ToBelegNrAsync_IncludesTenant_WhenRegisterNumbersMatch()
    {
        var database = $"BelegTenant_{Guid.NewGuid():N}";
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var registerA = Guid.NewGuid();
        var registerB = Guid.NewGuid();

        await using (var seedA = CreateDb(database, tenantA))
        {
            seedA.CashRegisters.Add(Register(tenantA, registerA));
            await seedA.SaveChangesAsync();
        }

        await using (var seedB = CreateDb(database, tenantB))
        {
            seedB.CashRegisters.Add(Register(tenantB, registerB));
            await seedB.SaveChangesAsync();
        }

        await using var ctxA = CreateDb(database, tenantA);
        await using var ctxB = CreateDb(database, tenantB);
        var numberA = await new SequenceReservationService(ctxA, NullLogger<SequenceReservationService>.Instance)
            .ToBelegNrAsync(registerA, 1);
        var numberB = await new SequenceReservationService(ctxB, NullLogger<SequenceReservationService>.Instance)
            .ToBelegNrAsync(registerB, 1);

        Assert.NotEqual(numberA, numberB);
        Assert.Contains(tenantA.ToString("N"), numberA, StringComparison.Ordinal);
        Assert.Contains(tenantB.ToString("N"), numberB, StringComparison.Ordinal);
        Assert.StartsWith("AT-KASSE-001-", numberA, StringComparison.Ordinal);
        Assert.StartsWith("AT-KASSE-001-", numberB, StringComparison.Ordinal);
    }

    private static CashRegister Register(Guid tenantId, Guid registerId) => new()
    {
        Id = registerId,
        TenantId = tenantId,
        RegisterNumber = "KASSE-001",
        Location = "Hauptkasse",
        StartingBalance = 0,
        CurrentBalance = 0,
        LastBalanceUpdate = DateTime.UtcNow,
        Status = RegisterStatus.Closed,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static AppDbContext CreateDb(string database, Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(database)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }
}
