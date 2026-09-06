using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyOperationHistoryServiceTests
{
    [Fact]
    public async Task List_Manager_SeesOnlyAmbientTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenantA);
        await using var _ = db;
        db.FiskalyOperationHistories.Add(Row(tenantA, FiskalyOperationTypes.Normal, "Success", "A-1"));
        db.FiskalyOperationHistories.Add(Row(tenantB, FiskalyOperationTypes.Cancel, "Failed", "B-1"));
        await db.SaveChangesAsync();

        var sut = CreateSut(db, accessor, Mock.Of<IFiskalyReceiptService>());
        var page = await sut.ListAsync(new FiskalyOperationHistoryQuery { Page = 1, PageSize = 50 }, actorIsSuperAdmin: false);

        Assert.Single(page.Items);
        Assert.Equal("A-1", page.Items[0].ReceiptNumber);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(100, page.Items[0].ProgressPercent);
    }

    [Fact]
    public async Task List_SuperAdmin_SeesAllTenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenantA);
        await using var _ = db;
        db.FiskalyOperationHistories.Add(Row(tenantA, FiskalyOperationTypes.Normal, "Success", "A-1"));
        db.FiskalyOperationHistories.Add(Row(tenantB, FiskalyOperationTypes.Nullbeleg, "Failed", "B-1"));
        await db.SaveChangesAsync();

        var sut = CreateSut(db, accessor, Mock.Of<IFiskalyReceiptService>());
        var page = await sut.ListAsync(new FiskalyOperationHistoryQuery { Page = 1, PageSize = 50 }, actorIsSuperAdmin: true);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(2, page.TotalCount);
    }

    [Fact]
    public async Task List_FiltersByTypeStatusAndSearch()
    {
        var tenant = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenant);
        await using var _ = db;
        db.FiskalyOperationHistories.Add(Row(tenant, FiskalyOperationTypes.Normal, "Success", "R-100", "alice"));
        db.FiskalyOperationHistories.Add(Row(tenant, FiskalyOperationTypes.Cancel, "Failed", "R-200", "bob"));
        await db.SaveChangesAsync();

        var sut = CreateSut(db, accessor, Mock.Of<IFiskalyReceiptService>());
        var page = await sut.ListAsync(
            new FiskalyOperationHistoryQuery
            {
                OperationType = FiskalyOperationTypes.Cancel,
                Status = "Failed",
                Search = "bob",
                Page = 1,
                PageSize = 25
            },
            actorIsSuperAdmin: false);

        Assert.Single(page.Items);
        Assert.Equal("R-200", page.Items[0].ReceiptNumber);
    }

    [Fact]
    public async Task List_CompletedFilter_MapsToSuccess()
    {
        var tenant = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenant);
        await using var _ = db;
        db.FiskalyOperationHistories.Add(Row(tenant, FiskalyOperationTypes.Normal, "Success", "R-100"));
        db.FiskalyOperationHistories.Add(Row(tenant, FiskalyOperationTypes.Cancel, "Failed", "R-200"));
        await db.SaveChangesAsync();

        var sut = CreateSut(db, accessor, Mock.Of<IFiskalyReceiptService>());
        var page = await sut.ListAsync(
            new FiskalyOperationHistoryQuery { Status = "Completed", Page = 1, PageSize = 25 },
            actorIsSuperAdmin: false);

        Assert.Single(page.Items);
        Assert.Equal("R-100", page.Items[0].ReceiptNumber);
        Assert.Equal("Success", page.Items[0].Status);
    }

    [Fact]
    public async Task Retry_Processing_Throws()
    {
        var tenant = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenant);
        await using var _ = db;
        var id = Guid.NewGuid();
        db.FiskalyOperationHistories.Add(Row(tenant, FiskalyOperationTypes.Normal, "Processing", "R-1", id: id));
        await db.SaveChangesAsync();

        var sut = CreateSut(db, accessor, Mock.Of<IFiskalyReceiptService>(MockBehavior.Strict));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.RetryAsync(id, "actor-1", actorIsSuperAdmin: false));
    }

    [Fact]
    public async Task GetById_OtherTenant_ReturnsNullForManager()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenantA);
        await using var _ = db;
        db.FiskalyOperationHistories.Add(Row(tenantB, FiskalyOperationTypes.Startbeleg, "Failed", "X", id: otherId));
        await db.SaveChangesAsync();

        var sut = CreateSut(db, accessor, Mock.Of<IFiskalyReceiptService>());
        var found = await sut.GetByIdAsync(otherId, actorIsSuperAdmin: false);
        Assert.Null(found);
    }

    [Fact]
    public async Task Retry_Failed_DelegatesToReceiptServiceAndIncrementsCount()
    {
        var tenant = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var originalId = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenant);
        await using var _ = db;
        db.FiskalyOperationHistories.Add(new FiskalyOperationHistory
        {
            Id = originalId,
            TenantId = tenant,
            OperationType = FiskalyOperationTypes.Normal,
            Status = nameof(FiskalyOperationHistoryStatus.Failed),
            CashRegisterId = registerId,
            UserId = "mgr-1",
            RequestPayloadJson = $$"""{"cashRegisterId":"{{registerId:D}}","amount":10.5,"vatRate":"STANDARD"}""",
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var receipts = new Mock<IFiskalyReceiptService>();
        receipts
            .Setup(s => s.CreateNormalReceiptAsync(
                registerId,
                10.5m,
                "STANDARD",
                "actor-1",
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiskalyReceiptOperationResult.Ok(new FiskalyReceiptDataDto
            {
                ReceiptId = "new-1",
                ReceiptNumber = "99"
            }));

        var sut = CreateSut(db, accessor, receipts.Object);
        var result = await sut.RetryAsync(originalId, "actor-1", actorIsSuperAdmin: false);

        Assert.True(result.Operation.Success);
        Assert.Equal("99", result.Operation.Data?.ReceiptNumber);
        var original = await db.FiskalyOperationHistories.AsNoTracking().SingleAsync(x => x.Id == originalId);
        Assert.Equal(1, original.RetryCount);
        receipts.VerifyAll();
    }

    [Fact]
    public async Task Retry_SuccessRow_Throws()
    {
        var tenant = Guid.NewGuid();
        var id = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenant);
        await using var _ = db;
        db.FiskalyOperationHistories.Add(Row(tenant, FiskalyOperationTypes.Normal, "Success", "1", id: id));
        await db.SaveChangesAsync();

        var sut = CreateSut(db, accessor, Mock.Of<IFiskalyReceiptService>(MockBehavior.Strict));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.RetryAsync(id, "actor-1", actorIsSuperAdmin: false));
    }

    [Fact]
    public async Task Retry_Missing_ThrowsKeyNotFound()
    {
        var (db, accessor) = CreateDb(Guid.NewGuid());
        await using var _ = db;
        var sut = CreateSut(db, accessor, Mock.Of<IFiskalyReceiptService>(MockBehavior.Strict));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.RetryAsync(Guid.NewGuid(), "actor-1", actorIsSuperAdmin: false));
    }

    [Fact]
    public void HistoryController_RequiresViewAndRetryPermissions()
    {
        var list = typeof(Controllers.AdminFiskalyHistoryController)
            .GetMethod(nameof(Controllers.AdminFiskalyHistoryController.List));
        var retry = typeof(Controllers.AdminFiskalyHistoryController)
            .GetMethod(nameof(Controllers.AdminFiskalyHistoryController.Retry));
        Assert.NotNull(list);
        Assert.NotNull(retry);
        Assert.Contains(
            AppPermissions.FiskalyHistoryView,
            list!.GetCustomAttributes<HasPermissionAttribute>().Select(a => a.Permission));
        Assert.Contains(
            AppPermissions.FiskalyHistoryRetry,
            retry!.GetCustomAttributes<HasPermissionAttribute>().Select(a => a.Permission));
    }

    private static FiskalyOperationHistoryService CreateSut(
        AppDbContext db,
        ICurrentTenantAccessor accessor,
        IFiskalyReceiptService receipts)
    {
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

        return new FiskalyOperationHistoryService(
            db,
            accessor,
            receipts,
            new FiskalyOperationHistoryWriteScope(),
            audit.Object,
            NullLogger<FiskalyOperationHistoryService>.Instance);
    }

    private static (AppDbContext Db, ICurrentTenantAccessor Accessor) CreateDb(Guid ambientTenant)
    {
        var accessor = TenantTestDoubles.TenantAccessorReturning(ambientTenant);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"fiskaly_hist_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return (new AppDbContext(options, accessor), accessor);
    }

    private static FiskalyOperationHistory Row(
        Guid tenantId,
        string operation,
        string status,
        string receiptNumber,
        string userDisplay = "user",
        Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            OperationType = operation,
            Status = status,
            CashRegisterId = Guid.NewGuid(),
            ReceiptNumber = receiptNumber,
            UserId = userDisplay,
            UserDisplayName = userDisplay,
            RequestPayloadJson = """{"cashRegisterId":"00000000-0000-0000-0000-000000000001"}""",
            CreatedAtUtc = DateTime.UtcNow
        };
}
