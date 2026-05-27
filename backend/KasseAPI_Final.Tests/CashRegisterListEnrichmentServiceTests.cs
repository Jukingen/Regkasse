using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.AdminCashRegisters;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CashRegisterListEnrichmentServiceTests
{
    private static IOptionsMonitor<TseOptions> CreateTseOptionsMonitor(string tseMode)
    {
        var monitor = new Mock<IOptionsMonitor<TseOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(new TseOptions { TseMode = tseMode });
        return monitor.Object;
    }

    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RegisterId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CashRegEnrich_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    [Theory]
    [InlineData(TseOperationalHealth.Online, true, "healthy")]
    [InlineData(TseOperationalHealth.Degraded, true, "degraded")]
    [InlineData(TseOperationalHealth.Offline, true, "offline")]
    [InlineData(TseOperationalHealth.Online, false, "notConfigured")]
    public void MapTseHealthStatus_maps_expected_values(
        TseOperationalHealth health,
        bool configured,
        string expected)
    {
        var snapshot = new TseHealthSnapshot
        {
            Status = health,
            LastCheckUtc = DateTime.UtcNow,
        };

        var health = new CashRegisterHealthService(
            CreateDb(),
            AlwaysOnlineTseHealthMonitor.Instance,
            CreateTseOptionsMonitor("Device"));
        var actual = health.MapTseHealthStatus(snapshot, configured);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ApplyAsync_sets_offline_queue_and_monatsbeleg_fields()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;
        db.Tenants.Add(new Tenant { Id = TenantId, Name = "T", Slug = "t", CreatedAt = now });
        db.CashRegisters.Add(new CashRegister
        {
            Id = RegisterId,
            TenantId = TenantId,
            RegisterNumber = "K1",
            Location = "Main",
            StartingBalance = 0m,
            CurrentBalance = 0m,
            LastBalanceUpdate = now,
            Status = RegisterStatus.Open,
            LastMonatsbelegUtc = now.AddDays(-3),
            LastJahresbelegUtc = now.AddDays(-30),
        });
        db.OfflineTransactions.Add(new OfflineTransaction
        {
            TenantId = TenantId,
            CashRegisterId = RegisterId,
            PayloadJson = "{}",
            ServerReceivedAtUtc = now,
            OfflineCreatedAtUtc = now,
            Status = OfflineTransactionStatus.Pending,
        });
        await db.SaveChangesAsync();

        var service = new CashRegisterHealthService(
            db,
            AlwaysOnlineTseHealthMonitor.Instance,
            CreateTseOptionsMonitor("Device"));

        var entity = await db.CashRegisters.Include(r => r.CurrentUser).FirstAsync();
        var dto = new CashRegisterDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            RegisterNumber = entity.RegisterNumber,
            Location = entity.Location,
            Status = entity.Status,
            StartingBalance = entity.StartingBalance,
            CurrentBalance = entity.CurrentBalance,
            LastBalanceUpdate = entity.LastBalanceUpdate,
            CreatedAt = entity.CreatedAt,
        };

        await service.ApplyOperationalFieldsAsync([dto], [entity], CancellationToken.None);

        Assert.Equal(now.AddDays(-3), dto.LastMonatsbelegUtc);
        Assert.Equal(now.AddDays(-30), dto.LastJahresbelegUtc);
        Assert.Equal(1, dto.OfflineQueueCount);
        Assert.Equal("healthy", dto.TseHealthStatus);
    }

    [Fact]
    public async Task GetTseHealthAsync_returns_404_when_register_missing()
    {
        await using var db = CreateDb();
        var service = new CashRegisterHealthService(
            db,
            AlwaysOnlineTseHealthMonitor.Instance,
            CreateTseOptionsMonitor("Device"));

        var result = await service.GetTseHealthForRegisterAsync(RegisterId, CancellationToken.None);
        Assert.Null(result);
    }
}
