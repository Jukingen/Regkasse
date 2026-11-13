using System.Runtime.CompilerServices;
using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Kitchen;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PosKitchenOrdersApiTests
{
    private static readonly ConditionalWeakTable<AppDbContext, ICurrentTenantAccessor> Accessors = new();

    [Fact]
    public async Task Create_ThenList_UpdateStatus_WritesAuditAndHubEvents()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        var registerId = SeedRegister(db, tenantId);
        await db.SaveChangesAsync();

        var audit = CreateAuditMock();
        var hub = new KitchenHubTestClient();
        var controller = CreateController(db, "cashier-1", tenantId, audit.Object, hub);

        var created = await controller.Create(
            new CreateKitchenOrderRequest
            {
                CashRegisterId = registerId,
                TableNumber = "12",
                Items =
                [
                    new CreateKitchenOrderItemRequest { ProductName = "Schnitzel", Quantity = 2 },
                ],
            },
            CancellationToken.None);

        var createdResult = Assert.IsType<CreatedResult>(created.Result);
        var dto = Assert.IsType<KitchenOrderDto>(createdResult.Value);
        Assert.Equal(KitchenOrderStatus.Pending, dto.Status);
        Assert.Equal("12", dto.TableNumber);
        Assert.Single(dto.Items);
        Assert.Single(hub.Created);
        Assert.Equal(dto.Id, hub.Created[0].Id);

        var list = await controller.List(status: null, from: null, to: null, CancellationToken.None);
        var listOk = Assert.IsType<OkObjectResult>(list.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<KitchenOrderDto>>(listOk.Value);
        Assert.Single(items);

        var patched = await controller.UpdateStatus(
            dto.Id,
            new UpdateKitchenOrderStatusRequest { Status = KitchenOrderStatus.Ready },
            CancellationToken.None);
        var patchedOk = Assert.IsType<OkObjectResult>(patched.Result);
        var ready = Assert.IsType<KitchenOrderDto>(patchedOk.Value);
        Assert.Equal(KitchenOrderStatus.Ready, ready.Status);
        Assert.NotNull(ready.ReadyAtUtc);
        Assert.Single(hub.StatusChanged);

        VerifyAudit(audit, AuditEventType.KitchenOrderCreated, Times.Once());
        VerifyAudit(audit, AuditEventType.KitchenOrderStatusChanged, Times.Once());
        Assert.Equal(126, (int)AuditEventType.KitchenOrderCreated);
        Assert.Equal(127, (int)AuditEventType.KitchenOrderStatusChanged);
        Assert.Equal(128, (int)AuditEventType.KitchenOrderCancelled);
    }

    [Fact]
    public async Task UpdateItemStatus_BroadcastsItemEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        var registerId = SeedRegister(db, tenantId);
        await db.SaveChangesAsync();

        var hub = new KitchenHubTestClient();
        var controller = CreateController(db, "cashier-1", tenantId, hub: hub);
        var created = await controller.Create(
            new CreateKitchenOrderRequest
            {
                CashRegisterId = registerId,
                Items = [new CreateKitchenOrderItemRequest { ProductName = "Salat", Quantity = 1 }],
            },
            CancellationToken.None);
        var dto = Assert.IsType<KitchenOrderDto>(Assert.IsType<CreatedResult>(created.Result).Value);
        var itemId = Assert.Single(dto.Items).Id;

        var patched = await controller.UpdateItemStatus(
            dto.Id,
            itemId,
            new UpdateKitchenOrderItemStatusRequest { Status = KitchenOrderItemStatus.Preparing },
            CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(patched.Result);
        var updated = Assert.IsType<KitchenOrderDto>(ok.Value);
        Assert.Equal(KitchenOrderItemStatus.Preparing, Assert.Single(updated.Items).Status);
        Assert.Single(hub.ItemChanged);
        Assert.Equal(itemId, hub.ItemChanged[0].ItemId);
    }

    [Fact]
    public async Task CrossTenant_ListUpdateDelete_Return404()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantA);
        SeedTenant(db, tenantB);
        var registerId = SeedRegister(db, tenantA);
        await db.SaveChangesAsync();

        var actorA = CreateController(db, "cashier-a", tenantA);
        var created = await actorA.Create(
            new CreateKitchenOrderRequest
            {
                CashRegisterId = registerId,
                Items = [new CreateKitchenOrderItemRequest { ProductName = "Suppe", Quantity = 1 }],
            },
            CancellationToken.None);
        var dto = Assert.IsType<KitchenOrderDto>(Assert.IsType<CreatedResult>(created.Result).Value);

        var actorB = CreateController(db, "cashier-b", tenantB);
        var list = await actorB.List(null, null, null, CancellationToken.None);
        var listOk = Assert.IsType<OkObjectResult>(list.Result);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<KitchenOrderDto>>(listOk.Value));

        var patch = await actorB.UpdateStatus(
            dto.Id,
            new UpdateKitchenOrderStatusRequest { Status = KitchenOrderStatus.Ready },
            CancellationToken.None);
        Assert.IsType<NotFoundResult>(patch.Result);

        var delete = await actorB.Cancel(dto.Id, CancellationToken.None);
        Assert.IsType<NotFoundResult>(delete.Result);
    }

    [Fact]
    public async Task Cancel_WritesCancelledAudit_AndRejectsServed()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        var registerId = SeedRegister(db, tenantId);
        await db.SaveChangesAsync();

        var audit = CreateAuditMock();
        var controller = CreateController(db, "cashier-1", tenantId, audit.Object);
        var created = await controller.Create(
            new CreateKitchenOrderRequest
            {
                CashRegisterId = registerId,
                Items = [new CreateKitchenOrderItemRequest { ProductName = "Pizza", Quantity = 1 }],
            },
            CancellationToken.None);
        var dto = Assert.IsType<KitchenOrderDto>(Assert.IsType<CreatedResult>(created.Result).Value);

        var cancelled = await controller.Cancel(dto.Id, CancellationToken.None);
        var cancelledOk = Assert.IsType<OkObjectResult>(cancelled.Result);
        Assert.Equal(KitchenOrderStatus.Cancelled, Assert.IsType<KitchenOrderDto>(cancelledOk.Value).Status);
        VerifyAudit(audit, AuditEventType.KitchenOrderCancelled, Times.Once());

        var servedCreate = await controller.Create(
            new CreateKitchenOrderRequest
            {
                CashRegisterId = registerId,
                Items = [new CreateKitchenOrderItemRequest { ProductName = "Pasta", Quantity = 1 }],
            },
            CancellationToken.None);
        var servedDto = Assert.IsType<KitchenOrderDto>(Assert.IsType<CreatedResult>(servedCreate.Result).Value);
        await controller.UpdateStatus(
            servedDto.Id,
            new UpdateKitchenOrderStatusRequest { Status = KitchenOrderStatus.Served },
            CancellationToken.None);
        var reject = await controller.Cancel(servedDto.Id, CancellationToken.None);
        var bad = Assert.IsType<ObjectResult>(reject.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
        Assert.Equal(KitchenOrderErrorCodes.AlreadyServed, Assert.IsType<KitchenOrderErrorDto>(bad.Value).Code);
    }

    [Fact]
    public async Task MissingTenant_Returns404()
    {
        await using var db = await CreateDbAsync(null);
        var controller = CreateController(db, "cashier-1", tenantId: null);
        var list = await controller.List(null, null, null, CancellationToken.None);
        Assert.IsType<NotFoundResult>(list.Result);
    }

    [Fact]
    public void CreateRequiresCartView_ListRequiresKitchenView()
    {
        var create = typeof(PosKitchenOrdersController)
            .GetMethod(nameof(PosKitchenOrdersController.Create))!
            .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
            .Cast<HasPermissionAttribute>()
            .Single();
        Assert.Equal(AppPermissions.CartView, create.Permission);

        var list = typeof(PosKitchenOrdersController)
            .GetMethod(nameof(PosKitchenOrdersController.List))!
            .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
            .Cast<HasPermissionAttribute>()
            .Single();
        Assert.Equal(AppPermissions.KitchenView, list.Permission);
    }

    [Fact]
    public void KitchenHub_IsTenantGroupOnly()
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Assert.Equal("/hubs/kitchen", KasseAPI_Final.Hubs.KitchenHub.HubPath);
        Assert.Equal($"kitchen:{tenantId:D}", KasseAPI_Final.Hubs.KitchenHub.TenantGroup(tenantId));
    }

    private static async Task<AppDbContext> CreateDbAsync(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"KitchenOrders_{Guid.NewGuid():N}")
            .Options;
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var db = new AppDbContext(options, accessor);
        Accessors.Add(db, accessor);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static PosKitchenOrdersController CreateController(
        AppDbContext db,
        string actorId,
        Guid? tenantId = null,
        IAuditLogService? audit = null,
        IKitchenOrderBroadcaster? hub = null)
    {
        var accessor = Accessors.GetValue(
            db,
            _ => throw new InvalidOperationException("Tenant accessor was not registered for the test context."));
        if (accessor is TenantTestDoubles.MutableTenantAccessor mutable)
            mutable.TenantId = tenantId ?? mutable.TenantId;

        var service = new KitchenOrderService(
            db,
            accessor,
            audit ?? CreateAuditMock().Object,
            hub ?? new KitchenHubTestClient(),
            Mock.Of<ILogger<KitchenOrderService>>());
        return new PosKitchenOrdersController(service, accessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, actorId),
                            new Claim(ClaimTypes.Role, Roles.Cashier),
                        ],
                        authenticationType: "test")),
                },
            },
        };
    }

    private static Guid SeedRegister(AppDbContext db, Guid tenantId)
    {
        SeedTenant(db, tenantId);
        var registerId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = $"K-{registerId:N}"[..8],
            Location = "Wien",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        return registerId;
    }

    private static void SeedTenant(AppDbContext db, Guid tenantId)
    {
        if (db.Tenants.Local.Any(tenant => tenant.Id == tenantId)
            || db.Tenants.IgnoreQueryFilters().Any(tenant => tenant.Id == tenantId))
            return;
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {tenantId:N}",
            Slug = $"t-{tenantId:N}"[..20],
            Status = TenantStatuses.Active,
            IsActive = true,
        });
    }

    private static Mock<IAuditLogService> CreateAuditMock()
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(logger => logger.LogSystemOperationAsync(
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
        return audit;
    }

    private static void VerifyAudit(Mock<IAuditLogService> audit, AuditEventType actionType, Times times)
    {
        audit.Verify(
            logger => logger.LogSystemOperationAsync(
                It.IsAny<string>(),
                nameof(KitchenOrder),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                actionType,
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            times);
    }
}

public sealed class KitchenHubTestClient : IKitchenOrderBroadcaster
{
    public List<KitchenOrderDto> Created { get; } = [];
    public List<KitchenOrderDto> StatusChanged { get; } = [];
    public List<(KitchenOrderDto Order, Guid ItemId)> ItemChanged { get; } = [];

    public Task OrderCreatedAsync(KitchenOrderDto order, CancellationToken cancellationToken)
    {
        Created.Add(order);
        return Task.CompletedTask;
    }

    public Task OrderStatusChangedAsync(KitchenOrderDto order, CancellationToken cancellationToken)
    {
        StatusChanged.Add(order);
        return Task.CompletedTask;
    }

    public Task ItemStatusChangedAsync(KitchenOrderDto order, Guid itemId, CancellationToken cancellationToken)
    {
        ItemChanged.Add((order, itemId));
        return Task.CompletedTask;
    }
}
