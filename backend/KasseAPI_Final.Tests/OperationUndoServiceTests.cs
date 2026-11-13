using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Operations;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OperationUndoServiceTests
{
    [Fact]
    public async Task Undo_UpdateProduct_RestoresBeforeState()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Demo",
            Slug = "demo",
            Status = TenantStatuses.Active,
        });

        var categoryId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Key = "food",
            Name = "Food",
            IsActive = true,
        });

        var productId = Guid.NewGuid();
        var product = new Product
        {
            Id = productId,
            TenantId = tenantId,
            Name = "After",
            Price = 20m,
            CategoryId = categoryId,
            Category = "Food",
            Barcode = "111",
            Unit = "Stk",
            IsActive = true,
        };
        db.Products.Add(product);

        var before = OperationSnapshots.FromProduct(new Product
        {
            Id = productId,
            TenantId = tenantId,
            Name = "Before",
            Price = 10m,
            CategoryId = categoryId,
            Category = "Food",
            Barcode = "111",
            Unit = "Stk",
            IsActive = true,
        });

        db.OperationLogs.Add(new OperationLog
        {
            TenantId = tenantId,
            UserId = "actor-1",
            OperationType = OperationTypes.UpdateProduct,
            EntityType = OperationEntityTypes.Product,
            EntityId = productId.ToString("D"),
            BeforeState = OperationSnapshots.Serialize(before),
            AfterState = OperationSnapshots.Serialize(OperationSnapshots.FromProduct(product)),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var opId = await db.OperationLogs.Select(o => o.Id).SingleAsync();
        var undo = CreateUndoService(db);

        var result = await undo.UndoOperationAsync(tenantId, opId, "undo-user");

        Assert.True(result.Success, result.Message);
        var restored = await db.Products.SingleAsync(p => p.Id == productId);
        Assert.Equal("Before", restored.Name);
        Assert.Equal(10m, restored.Price);
        Assert.True(await db.OperationLogs.Where(o => o.Id == opId).Select(o => o.IsUndone).SingleAsync());
    }

    [Fact]
    public async Task Undo_CreatePayment_IsRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Demo",
            Slug = "demo",
            Status = TenantStatuses.Active,
        });

        db.OperationLogs.Add(new OperationLog
        {
            TenantId = tenantId,
            UserId = "actor-1",
            OperationType = OperationTypes.CreatePayment,
            EntityType = OperationEntityTypes.Payment,
            EntityId = Guid.NewGuid().ToString("D"),
            BeforeState = null,
            AfterState = "{}",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var opId = await db.OperationLogs.Select(o => o.Id).SingleAsync();
        var undo = CreateUndoService(db);

        var result = await undo.UndoOperationAsync(tenantId, opId, "undo-user");

        Assert.False(result.Success);
        Assert.Equal("NOT_UNDOABLE", result.ErrorCode);
        Assert.False(await db.OperationLogs.Where(o => o.Id == opId).Select(o => o.IsUndone).SingleAsync());
    }

    [Fact]
    public async Task Undo_AlreadyUndone_Fails()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Demo",
            Slug = "demo",
            Status = TenantStatuses.Active,
        });

        db.OperationLogs.Add(new OperationLog
        {
            TenantId = tenantId,
            UserId = "actor-1",
            OperationType = OperationTypes.UpdateProduct,
            EntityType = OperationEntityTypes.Product,
            EntityId = Guid.NewGuid().ToString("D"),
            BeforeState = "{}",
            IsUndone = true,
            UndoneBy = "other",
            UndoneAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var opId = await db.OperationLogs.Select(o => o.Id).SingleAsync();
        var undo = CreateUndoService(db);

        var result = await undo.UndoOperationAsync(tenantId, opId, "undo-user");

        Assert.False(result.Success);
        Assert.Equal("ALREADY_UNDONE", result.ErrorCode);
    }

    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"op-undo-{Guid.NewGuid():N}")
            .Options;
        var tenantAccessor = Mock.Of<ICurrentTenantAccessor>(a => a.TenantId == tenantId);
        return new AppDbContext(options, tenantAccessor);
    }

    private static OperationUndoService CreateUndoService(AppDbContext db)
    {
        var logs = new OperationLogService(db, Mock.Of<IHttpContextAccessor>());
        var grace = Options.Create(new GracePeriodsOptions { Enabled = false });
        var graceMonitor = Mock.Of<IOptionsMonitor<GracePeriodsOptions>>(m => m.CurrentValue == grace.Value);
        // Undo wraps audit failures; an unconfigured mock is enough.
        return new OperationUndoService(
            db,
            logs,
            Mock.Of<IAuditLogService>(),
            graceMonitor,
            NullLogger<OperationUndoService>.Instance);
    }
}
