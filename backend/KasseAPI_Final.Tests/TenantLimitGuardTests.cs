using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.Limits;
using KasseAPI_Final.Services.Metrics;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantLimitGuardTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TenantLimitGuard_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static TenantLimitGuard CreateGuard(AppDbContext db)
    {
        var cache = new TenantLimitCacheService(
            new MemoryCacheService(
                new MemoryCache(new MemoryCacheOptions()),
                NullLogger<MemoryCacheService>.Instance,
                new CacheMetricsService()),
            Options.Create(new KasseAPI_Final.Configuration.CacheSettings()),
            NullLogger<TenantLimitCacheService>.Instance);
        var limits = new TenantLimitService(db, cache, NullLogger<TenantLimitService>.Instance);
        return new TenantLimitGuard(db, limits, NullLogger<TenantLimitGuard>.Instance);
    }

    private static async Task SeedTenantAsync(AppDbContext db, Action<TenantLimits>? configure = null)
    {
        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Name = "Limits tenant",
            Slug = "limits-guard",
            IsActive = true,
            Status = TenantStatuses.Active,
            CreatedAt = DateTime.UtcNow,
        });
        var caps = TenantLimits.CreateDefault(TenantId);
        configure?.Invoke(caps);
        db.TenantLimits.Add(caps);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task EnsureCanCreateProductAsync_WhenAtCap_Throws()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, c => c.MaxProductsPerTenant = 1);
        db.Products.Add(NewProduct("P1"));
        await db.SaveChangesAsync();
        var guard = CreateGuard(db);

        var ex = await Assert.ThrowsAsync<LimitExceededException>(
            () => guard.EnsureCanCreateProductAsync(TenantId));

        Assert.Equal(TenantLimitKeys.MaxProductsPerTenant, ex.LimitKey);
        Assert.Equal(LimitExceededException.ErrorCodeValue, ex.ErrorCode);
        Assert.Equal(1, ex.CurrentValue);
    }

    [Fact]
    public async Task EnsureCanCreateProductAsync_WhenInactiveDoesNotCount_Allows()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, c => c.MaxProductsPerTenant = 1);
        var inactive = NewProduct("P1");
        inactive.IsActive = false;
        db.Products.Add(inactive);
        await db.SaveChangesAsync();
        var guard = CreateGuard(db);

        await guard.EnsureCanCreateProductAsync(TenantId);
    }

    [Fact]
    public async Task EnsureCanCreateUserAsync_WhenAtCap_Throws()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, c => c.MaxUsersPerTenant = 1);
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            TenantId = TenantId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var guard = CreateGuard(db);

        var ex = await Assert.ThrowsAsync<LimitExceededException>(
            () => guard.EnsureCanCreateUserAsync(TenantId));

        Assert.Equal(TenantLimitKeys.MaxUsersPerTenant, ex.LimitKey);
        Assert.Equal(1, ex.Limit);
    }

    [Fact]
    public async Task EnsureSaleWithinLimitsAsync_DailyTransactionCap_Throws()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, c => c.DailyMaxTransactions = 1);
        var registerId = await SeedRegisterAsync(db);
        db.PaymentDetails.Add(NewPayment(registerId, 10m));
        await db.SaveChangesAsync();
        var guard = CreateGuard(db);

        var ex = await Assert.ThrowsAsync<LimitExceededException>(
            () => guard.EnsureSaleWithinLimitsAsync(TenantId, 5m));

        Assert.Equal(TenantLimitKeys.DailyMaxTransactions, ex.LimitKey);
        Assert.Equal(LimitExceededException.ErrorCodeValue, ex.ErrorCode);
    }

    [Fact]
    public async Task EnsureSaleWithinLimitsAsync_MaxAmount_Throws()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, c => c.MaxTransactionAmount = 20m);
        await SeedRegisterAsync(db);
        var guard = CreateGuard(db);

        var ex = await Assert.ThrowsAsync<LimitExceededException>(
            () => guard.EnsureSaleWithinLimitsAsync(TenantId, 20.01m));

        Assert.Equal(TenantLimitKeys.MaxTransactionAmount, ex.LimitKey);
        Assert.Equal(LimitExceededException.ErrorCodeValue, ex.ErrorCode);
    }

    [Fact]
    public async Task EnsureSaleWithinLimitsAsync_DailyRevenueCap_Throws()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, c =>
        {
            c.DailyMaxTransactions = 100;
            c.MaxTransactionAmount = 1000m;
            c.DailyMaxRevenue = 50m;
        });
        var registerId = await SeedRegisterAsync(db);
        db.PaymentDetails.Add(NewPayment(registerId, 40m));
        await db.SaveChangesAsync();
        var guard = CreateGuard(db);

        var ex = await Assert.ThrowsAsync<LimitExceededException>(
            () => guard.EnsureSaleWithinLimitsAsync(TenantId, 11m));

        Assert.Equal(TenantLimitKeys.DailyMaxRevenue, ex.LimitKey);
        Assert.Equal(LimitExceededException.ErrorCodeValue, ex.ErrorCode);
    }

    [Fact]
    public async Task EnsureCanCreateBackupAsync_WhenSucceededTenantCountAtCap_Throws()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, c => c.MaxBackupsPerTenant = 1);
        db.BackupRuns.Add(NewSucceededTenantBackup(TenantId));
        await db.SaveChangesAsync();
        var guard = CreateGuard(db);

        var ex = await Assert.ThrowsAsync<LimitExceededException>(
            () => guard.EnsureCanCreateBackupAsync(TenantId));

        Assert.Equal(TenantLimitKeys.MaxBackupsPerTenant, ex.LimitKey);
        Assert.Equal(1, ex.CurrentValue);
    }

    [Fact]
    public async Task EnsureCanCreateBackupAsync_SystemAndFailedRunsDoNotCount_Allows()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, c => c.MaxBackupsPerTenant = 1);
        var system = NewSucceededTenantBackup(TenantId);
        system.Strategy = BackupStrategyKind.System;
        db.BackupRuns.Add(system);
        var failed = NewSucceededTenantBackup(TenantId);
        failed.Status = BackupRunStatus.Failed;
        db.BackupRuns.Add(failed);
        await db.SaveChangesAsync();
        var guard = CreateGuard(db);

        await guard.EnsureCanCreateBackupAsync(TenantId);
    }

    [Fact]
    public async Task EnsureCanCreateBackupAsync_WhenEstimatedSizeWouldExceed_Throws()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, c =>
        {
            c.MaxBackupsPerTenant = 50;
            c.MaxBackupSizeMb = 1;
        });
        var run = NewSucceededTenantBackup(TenantId);
        run.Artifacts.Add(NewLogicalDump(600 * 1024));
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync();
        var guard = CreateGuard(db);

        var ex = await Assert.ThrowsAsync<LimitExceededException>(
            () => guard.EnsureCanCreateBackupAsync(TenantId));

        Assert.Equal(TenantLimitKeys.MaxBackupSizeMb, ex.LimitKey);
    }

    [Fact]
    public async Task EnsureCanCreateBackupAsync_FirstBackupWithEmptyHistory_Allows()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, c => c.MaxBackupSizeMb = 1);
        var guard = CreateGuard(db);

        await guard.EnsureCanCreateBackupAsync(TenantId);
    }

    [Fact]
    public async Task EnsureCanQueueOfflineTransactionAsync_PendingAndNonFiscalCountTowardCap()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, c => c.MaxOfflineTransactions = 2);
        var registerId = await SeedRegisterAsync(db);
        db.OfflineTransactions.Add(NewOffline(registerId, OfflineTransactionStatus.Pending));
        db.OfflineTransactions.Add(NewOffline(registerId, OfflineTransactionStatus.NonFiscalPending));
        await db.SaveChangesAsync();
        var guard = CreateGuard(db);

        var ex = await Assert.ThrowsAsync<LimitExceededException>(
            () => guard.EnsureCanQueueOfflineTransactionAsync(TenantId));

        Assert.Equal(TenantLimitKeys.MaxOfflineTransactions, ex.LimitKey);
        Assert.Equal(2, ex.CurrentValue);
    }

    [Fact]
    public async Task EnsureCanQueueOfflineTransactionAsync_SyncedDoesNotCount_Allows()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db, c => c.MaxOfflineTransactions = 1);
        var registerId = await SeedRegisterAsync(db);
        db.OfflineTransactions.Add(NewOffline(registerId, OfflineTransactionStatus.Synced));
        await db.SaveChangesAsync();
        var guard = CreateGuard(db);

        await guard.EnsureCanQueueOfflineTransactionAsync(TenantId);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsCounts()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db);
        db.Products.Add(NewProduct("P1"));
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            TenantId = TenantId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        var registerId = await SeedRegisterAsync(db);
        db.PaymentDetails.Add(NewPayment(registerId, 12.5m));
        await db.SaveChangesAsync();
        var guard = CreateGuard(db);

        var usage = await guard.GetUsageAsync(TenantId);

        Assert.Equal(1, usage.CurrentProducts);
        Assert.Equal(1, usage.CurrentUsers);
        Assert.Equal(1, usage.CurrentDailyTransactions);
        Assert.Equal(12.5m, usage.CurrentDailyRevenue);
        Assert.Equal(0, usage.CurrentBackups);
        Assert.Equal(0, usage.CurrentOfflineTransactions);
        Assert.Equal(TenantId, usage.TenantId);
    }

    private static async Task<Guid> SeedRegisterAsync(AppDbContext db)
    {
        var registerId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = TenantId,
            RegisterNumber = "KASSE-L",
            Location = "Wien",
            Status = RegisterStatus.Open,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return registerId;
    }

    private static Product NewProduct(string name) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        Name = name,
        Price = 1m,
        Category = "C",
        CategoryId = Guid.NewGuid(),
        StockQuantity = 1,
        MinStockLevel = 0,
        Unit = "Stk",
        TaxType = 2,
        TaxRate = 10m,
        Barcode = $"bc-{Guid.NewGuid():N}",
        IsFiscalCompliant = true,
        IsTaxable = true,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static PaymentDetails NewPayment(Guid registerId, decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        CustomerName = "Gast",
        TableNumber = 1,
        CashierId = "c1",
        TotalAmount = amount,
        TaxAmount = 0m,
        PaymentMethodRaw = "0",
        Steuernummer = "ATU12345678",
        CashRegisterId = registerId,
        TseSignature = "sig",
        TseTimestamp = DateTime.UtcNow,
        ReceiptNumber = $"R-{Guid.NewGuid():N}"[..20],
        CreatedAt = DateTime.UtcNow,
        IsActive = true,
    };

    private static BackupRun NewSucceededTenantBackup(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Status = BackupRunStatus.Succeeded,
        Strategy = BackupStrategyKind.Tenant,
        TriggerSource = BackupTriggerSource.Manual,
        AdapterKind = "Fake",
        RequestedAt = DateTime.UtcNow,
    };

    private static BackupArtifact NewLogicalDump(long byteSize) => new()
    {
        Id = Guid.NewGuid(),
        ArtifactType = BackupArtifactType.LogicalDump,
        StorageDescriptor = $"dump-{Guid.NewGuid():N}.zip",
        ByteSize = byteSize,
        CreatedAt = DateTime.UtcNow,
    };

    private static OfflineTransaction NewOffline(Guid registerId, OfflineTransactionStatus status) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        CashRegisterId = registerId,
        PayloadJson = "{}",
        PayloadHash = Guid.NewGuid().ToString("N"),
        ServerReceivedAtUtc = DateTime.UtcNow,
        OfflineCreatedAtUtc = DateTime.UtcNow,
        Status = status,
        CreatedBy = "c1",
        RetryCount = 0,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };
}
