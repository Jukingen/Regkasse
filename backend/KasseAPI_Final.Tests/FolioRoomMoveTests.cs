using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Lodging;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FolioRoomMoveTests
{
    [Fact]
    public async Task Move_OpenFolioToAvailableRoom_KeepsBalanceAndWritesAudit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        SeedTenant(db, tenantId);
        var oldRoom = await SeedRoomAsync(db, tenantId, "101", RoomStatus.Occupied);
        var newRoom = await SeedRoomAsync(db, tenantId, "102", RoomStatus.Available);
        var customerId = await SeedCustomerAsync(db, tenantId);
        var folio = await SeedFolioAsync(db, tenantId, customerId, oldRoom, GuestFolioStatus.Open, 42.50m, "breakfast");
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());
        var controller = CreateController(db, tenantId, audit.Object, "desk-1");

        var result = await controller.MoveRoom(
            folio.Id,
            new MoveFolioRoomRequest { NewRoomId = newRoom },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<GuestFolioDto>(ok.Value);
        Assert.Equal(newRoom, dto.RoomId);
        Assert.Equal(42.50m, dto.Balance);
        Assert.Contains("Zimmerwechsel: 101 → 102", dto.Notes, StringComparison.Ordinal);
        Assert.Contains("breakfast", dto.Notes, StringComparison.Ordinal);

        Assert.Equal(RoomStatus.Cleaning, (await db.Rooms.SingleAsync(row => row.Id == oldRoom)).Status);
        Assert.Equal(RoomStatus.Occupied, (await db.Rooms.SingleAsync(row => row.Id == newRoom)).Status);
        Assert.Equal(newRoom, (await db.GuestFolios.SingleAsync(row => row.Id == folio.Id)).RoomId);
        Assert.Equal(0, await db.GuestFolioItems.CountAsync());
        Assert.Equal(0, await db.PaymentDetails.CountAsync());

        audit.Verify(a => a.LogSystemOperationAsync(
                "FOLIO_ROOM_MOVED",
                "GuestFolio",
                "desk-1",
                Roles.Cashier,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.FolioRoomMoved,
                folio.Id,
                tenantId,
                It.IsAny<object?>(),
                It.Is<object?>(values => PayloadHasMove(values, tenantId, folio.Id, oldRoom, newRoom, "desk-1")),
                It.IsAny<string?>()),
            Times.Once);
        Assert.Equal(150, (int)AuditEventType.FolioRoomMoved);
    }

    [Theory]
    [InlineData(RoomStatus.Occupied)]
    [InlineData(RoomStatus.Cleaning)]
    public async Task Move_TargetNotAvailable_Returns409(RoomStatus status)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        SeedTenant(db, tenantId);
        var oldRoom = await SeedRoomAsync(db, tenantId, "101", RoomStatus.Occupied);
        var blocked = await SeedRoomAsync(db, tenantId, "102", status);
        var customerId = await SeedCustomerAsync(db, tenantId);
        var folio = await SeedFolioAsync(db, tenantId, customerId, oldRoom, GuestFolioStatus.Open, 10m, null);

        var result = await CreateController(db, tenantId).MoveRoom(
            folio.Id,
            new MoveFolioRoomRequest { NewRoomId = blocked },
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var error = Assert.IsType<LodgingErrorDto>(conflict.Value);
        Assert.Equal(LodgingErrorCodes.RoomNotAvailable, error.Code);
        Assert.Equal(oldRoom, (await db.GuestFolios.SingleAsync()).RoomId);
        Assert.Equal(status, (await db.Rooms.SingleAsync(row => row.Id == blocked)).Status);
    }

    [Fact]
    public async Task Move_ClosedFolio_Returns409()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        SeedTenant(db, tenantId);
        var oldRoom = await SeedRoomAsync(db, tenantId, "101", RoomStatus.Cleaning);
        var next = await SeedRoomAsync(db, tenantId, "102", RoomStatus.Available);
        var customerId = await SeedCustomerAsync(db, tenantId);
        var folio = await SeedFolioAsync(db, tenantId, customerId, oldRoom, GuestFolioStatus.Closed, 0m, null);

        var result = await CreateController(db, tenantId).MoveRoom(
            folio.Id,
            new MoveFolioRoomRequest { NewRoomId = next },
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(LodgingErrorCodes.FolioNotOpen, Assert.IsType<LodgingErrorDto>(conflict.Value).Code);
        Assert.Equal(RoomStatus.Available, (await db.Rooms.SingleAsync(row => row.Id == next)).Status);
        Assert.Equal(oldRoom, (await db.GuestFolios.SingleAsync()).RoomId);
    }

    [Fact]
    public async Task Move_CrossTenantRoom_Returns404()
    {
        var tenantId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        SeedTenant(db, tenantId);
        SeedTenant(db, otherId);
        var oldRoom = await SeedRoomAsync(db, tenantId, "101", RoomStatus.Occupied);
        var foreign = await SeedRoomAsync(db, otherId, "9", RoomStatus.Available);
        var customerId = await SeedCustomerAsync(db, tenantId);
        var folio = await SeedFolioAsync(db, tenantId, customerId, oldRoom, GuestFolioStatus.Open, 5m, null);

        var result = await CreateController(db, tenantId).MoveRoom(
            folio.Id,
            new MoveFolioRoomRequest { NewRoomId = foreign },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Equal(oldRoom, (await db.GuestFolios.IgnoreQueryFilters().SingleAsync()).RoomId);
        Assert.Equal(RoomStatus.Occupied, (await db.Rooms.IgnoreQueryFilters().SingleAsync(row => row.Id == oldRoom)).Status);
    }

    [Fact]
    public async Task Move_SameRoom_IsNoOp()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        SeedTenant(db, tenantId);
        var roomId = await SeedRoomAsync(db, tenantId, "101", RoomStatus.Occupied);
        var customerId = await SeedCustomerAsync(db, tenantId);
        var folio = await SeedFolioAsync(db, tenantId, customerId, roomId, GuestFolioStatus.Open, 8m, "stay");
        var audit = new Mock<IAuditLogService>();
        var controller = CreateController(db, tenantId, audit.Object);

        var result = await controller.MoveRoom(
            folio.Id,
            new MoveFolioRoomRequest { NewRoomId = roomId },
            CancellationToken.None);

        var dto = Assert.IsType<GuestFolioDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(roomId, dto.RoomId);
        Assert.Equal("stay", dto.Notes);
        Assert.Equal(RoomStatus.Occupied, (await db.Rooms.SingleAsync()).Status);
        audit.Verify(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()),
            Times.Never);
    }

    private static bool PayloadHasMove(
        object? values,
        Guid tenantId,
        Guid folioId,
        Guid oldRoomId,
        Guid newRoomId,
        string actorUserId)
    {
        if (values is null)
            return false;
        var json = JsonSerializer.Serialize(values);
        return json.Contains(tenantId.ToString(), StringComparison.OrdinalIgnoreCase)
            && json.Contains(folioId.ToString(), StringComparison.OrdinalIgnoreCase)
            && json.Contains(oldRoomId.ToString(), StringComparison.OrdinalIgnoreCase)
            && json.Contains(newRoomId.ToString(), StringComparison.OrdinalIgnoreCase)
            && json.Contains(actorUserId, StringComparison.Ordinal);
    }

    private static PosRoomsController CreateController(
        AppDbContext db,
        Guid tenantId,
        IAuditLogService? audit = null,
        string? userId = null)
    {
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var claims = new List<Claim> { new(ClaimTypes.Role, Roles.Cashier) };
        if (!string.IsNullOrWhiteSpace(userId))
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        return new PosRoomsController(new LodgingService(db, audit), accessor, new PermissiveVerticalProfileGuard())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test")),
                },
            },
        };
    }

    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"FolioRoomMove_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static void SeedTenant(AppDbContext db, Guid tenantId)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {tenantId:N}",
            Slug = $"t-{tenantId:N}"[..20],
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        db.SaveChanges();
    }

    private static async Task<Guid> SeedRoomAsync(AppDbContext db, Guid tenantId, string number, RoomStatus status)
    {
        var room = new Room
        {
            TenantId = tenantId,
            Number = number,
            Type = "DZ",
            Capacity = 2,
            Status = status,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return room.Id;
    }

    private static async Task<Guid> SeedCustomerAsync(AppDbContext db, Guid tenantId)
    {
        var customer = new Customer
        {
            TenantId = tenantId,
            Name = "Anna Gast",
            CustomerNumber = $"C-{Guid.NewGuid():N}"[..12],
            Email = $"{Guid.NewGuid():N}@example.test",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer.Id;
    }

    private static async Task<GuestFolio> SeedFolioAsync(
        AppDbContext db,
        Guid tenantId,
        Guid customerId,
        Guid roomId,
        GuestFolioStatus status,
        decimal balance,
        string? notes)
    {
        var folio = new GuestFolio
        {
            TenantId = tenantId,
            CustomerId = customerId,
            RoomId = roomId,
            CheckIn = DateTime.UtcNow.AddDays(-1),
            Status = status,
            Balance = balance,
            Notes = notes,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.GuestFolios.Add(folio);
        await db.SaveChangesAsync();
        return folio;
    }
}
