using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.DataRetention;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvDataCleanupServiceTests
{
    [Fact]
    public async Task Cleanup_WhenDisabled_ReturnsEarly()
    {
        var (factory, _) = CreateDb();
        var sut = CreateSut(factory, enabled: false);

        var result = await sut.CleanupRksvDataAsync();

        Assert.Equal(0, result.ArchivedCount);
        Assert.Contains("disabled", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cleanup_ArchivesPastRetentionPayments_AndKeepsLiveRows()
    {
        var (factory, db) = CreateDb();
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var ancient = DateTime.UtcNow.AddYears(-8);

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "Main",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = paymentId,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Walk-in",
            TableNumber = 1,
            CashierId = "c1",
            TotalAmount = 10m,
            TaxAmount = 1m,
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            CreatedAt = ancient,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var tempRoot = Path.Combine(Path.GetTempPath(), "rksv-cold-" + Guid.NewGuid().ToString("N"));
        var sut = CreateSut(factory, enabled: true, archiveRoot: tempRoot);

        try
        {
            var result = await sut.CleanupRksvDataAsync();

            Assert.Equal(1, result.EligibleCount);
            Assert.Equal(1, result.ArchivedCount);
            Assert.True(File.Exists(result.ArchivePath));
            Assert.False(result.HardDeleteRefused);

            await using var check = factory.CreateDbContext();
            Assert.Equal(1, await check.PaymentDetails.CountAsync(p => p.Id == paymentId));
            Assert.Equal(1, await check.RksvColdArchiveItems.CountAsync(i => i.PaymentDetailId == paymentId));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Cleanup_WhenHardDeleteEnabled_RefusesLiveDeletion()
    {
        var (factory, _) = CreateDb();
        var tempRoot = Path.Combine(Path.GetTempPath(), "rksv-cold-" + Guid.NewGuid().ToString("N"));
        var sut = CreateSut(factory, enabled: true, hardDelete: true, archiveRoot: tempRoot);

        try
        {
            var result = await sut.CleanupRksvDataAsync();
            Assert.True(result.HardDeleteRefused);
            Assert.Contains("refused", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static RksvDataCleanupService CreateSut(
        IDbContextFactory<AppDbContext> factory,
        bool enabled,
        bool hardDelete = false,
        string? archiveRoot = null)
    {
        var opts = Options.Create(new RksvDataCleanupOptions
        {
            Enabled = enabled,
            HardDeleteEnabled = hardDelete,
            RetentionYears = 7,
            ExtraArchiveYears = 3,
            MaxBatchSize = 100,
            ArchiveRootRelativeDirectory = archiveRoot ?? "App_Data/rksv-cold-archives",
        });
        var monitor = Mock.Of<IOptionsMonitor<RksvDataCleanupOptions>>(m => m.CurrentValue == opts.Value);

        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(Path.GetTempPath());

        return new RksvDataCleanupService(
            factory,
            monitor,
            env.Object,
            NullLogger<RksvDataCleanupService>.Instance);
    }

    private static (IDbContextFactory<AppDbContext> Factory, AppDbContext Db) CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return (new Factory(options), new AppDbContext(options));
    }

    private sealed class Factory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public Factory(DbContextOptions<AppDbContext> options) => _options = options;

        public AppDbContext CreateDbContext() => new(_options);

        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            new(new AppDbContext(_options));
    }
}
