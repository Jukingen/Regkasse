using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Lodging;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LodgingApiTests
{
    [Fact]
    public async Task Admin_CreateThenListRooms_ReturnsRoom()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        SeedTenant(db, tenantId);
        var controller = CreateAdminController(db, tenantId);

        var created = await controller.CreateRoom(
            new CreateRoomRequest { Number = "101", Type = "DZ", Capacity = 2 },
            CancellationToken.None);
        var createdResult = Assert.IsType<ObjectResult>(created.Result);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
        var room = Assert.IsType<RoomDto>(createdResult.Value);
        Assert.Equal("101", room.Number);
        Assert.False(room.Occupied);

        var list = await controller.ListRooms(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(list.Result);
        var rooms = Assert.IsAssignableFrom<IReadOnlyList<RoomDto>>(ok.Value);
        Assert.Single(rooms);
        Assert.Equal(room.Id, rooms[0].Id);
    }

    [Fact]
    public async Task Admin_DuplicateRoomNumber_Returns409()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        SeedTenant(db, tenantId);
        var controller = CreateAdminController(db, tenantId);
        var request = new CreateRoomRequest { Number = "102", Type = "EZ", Capacity = 1 };

        var first = await controller.CreateRoom(request, CancellationToken.None);
        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ObjectResult>(first.Result).StatusCode);

        var second = await controller.CreateRoom(request, CancellationToken.None);
        var conflict = Assert.IsAssignableFrom<ObjectResult>(second.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        var error = Assert.IsType<LodgingErrorDto>(conflict.Value);
        Assert.Equal(LodgingErrorCodes.RoomNumberDuplicate, error.Code);
    }

    [Fact]
    public async Task Pos_CreateFolioThenList_ReturnsOpenFolio()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        SeedTenant(db, tenantId);
        var roomId = await SeedRoomAsync(db, tenantId, "201");
        var customerId = await SeedCustomerAsync(db, tenantId, "Anna Gast");
        var controller = CreatePosController(db, tenantId);

        var created = await controller.CreateFolio(
            new CreateGuestFolioRequest { CustomerId = customerId, RoomId = roomId },
            CancellationToken.None);
        var createdResult = Assert.IsType<ObjectResult>(created.Result);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
        var folio = Assert.IsType<GuestFolioDto>(createdResult.Value);
        Assert.Equal("Anna Gast", folio.CustomerName);
        Assert.Equal("201", folio.RoomNumber);
        Assert.True(folio.IsOpen);

        var list = await controller.ListFolios(roomId, openOnly: true, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(list.Result);
        var folios = Assert.IsAssignableFrom<IReadOnlyList<GuestFolioDto>>(ok.Value);
        Assert.Single(folios);
        Assert.Equal(folio.Id, folios[0].Id);

        var rooms = await controller.ListRooms(CancellationToken.None);
        var roomOk = Assert.IsType<OkObjectResult>(rooms.Result);
        var room = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<RoomDto>>(roomOk.Value));
        Assert.True(room.Occupied);
    }

    [Fact]
    public async Task Pos_OccupiedRoom_Returns409()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        SeedTenant(db, tenantId);
        var roomId = await SeedRoomAsync(db, tenantId, "301");
        var firstGuest = await SeedCustomerAsync(db, tenantId, "Erst Gast");
        var secondGuest = await SeedCustomerAsync(db, tenantId, "Zweit Gast");
        var controller = CreatePosController(db, tenantId);

        var first = await controller.CreateFolio(
            new CreateGuestFolioRequest { CustomerId = firstGuest, RoomId = roomId },
            CancellationToken.None);
        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ObjectResult>(first.Result).StatusCode);

        var second = await controller.CreateFolio(
            new CreateGuestFolioRequest { CustomerId = secondGuest, RoomId = roomId },
            CancellationToken.None);
        var conflict = Assert.IsAssignableFrom<ObjectResult>(second.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal(LodgingErrorCodes.RoomOccupied, Assert.IsType<LodgingErrorDto>(conflict.Value).Code);
    }

    [Fact]
    public async Task Pos_InvalidStay_Returns400()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        SeedTenant(db, tenantId);
        var roomId = await SeedRoomAsync(db, tenantId, "401");
        var customerId = await SeedCustomerAsync(db, tenantId, "Kurz Gast");
        var controller = CreatePosController(db, tenantId);
        var checkIn = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

        var result = await controller.CreateFolio(
            new CreateGuestFolioRequest
            {
                CustomerId = customerId,
                RoomId = roomId,
                CheckIn = checkIn,
                CheckOut = checkIn,
            },
            CancellationToken.None);

        var bad = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
        Assert.Equal(LodgingErrorCodes.InvalidStay, Assert.IsType<LodgingErrorDto>(bad.Value).Code);
    }

    [Fact]
    public async Task Pos_UnknownRoom_Returns404()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        SeedTenant(db, tenantId);
        var customerId = await SeedCustomerAsync(db, tenantId, "Ohne Zimmer");
        var controller = CreatePosController(db, tenantId);

        var result = await controller.CreateFolio(
            new CreateGuestFolioRequest { CustomerId = customerId, RoomId = Guid.NewGuid() },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task MissingTenant_Returns404()
    {
        await using var db = CreateDb(Guid.Empty);
        var admin = CreateAdminController(db, Guid.Empty);
        Assert.IsType<NotFoundResult>((await admin.ListRooms(CancellationToken.None)).Result);
        var pos = CreatePosController(db, Guid.Empty);
        Assert.IsType<NotFoundResult>((await pos.ListRooms(CancellationToken.None)).Result);
    }

    [Fact]
    public async Task Pos_ChargeDoesNotCreatePayment_AndCloseClearsFolio()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        SeedTenant(db, tenantId);
        var roomId = await SeedRoomAsync(db, tenantId, "501");
        var customerId = await SeedCustomerAsync(db, tenantId, "Rechnung Gast");
        var controller = CreatePosController(db, tenantId);

        var created = await controller.CreateFolio(
            new CreateGuestFolioRequest { CustomerId = customerId, RoomId = roomId },
            CancellationToken.None);
        var folio = Assert.IsType<GuestFolioDto>(Assert.IsType<ObjectResult>(created.Result).Value);

        var charged = await controller.ChargeFolio(
            folio.Id,
            new ChargeFolioRequest { Description = "Frühstück", Amount = 12.50m },
            CancellationToken.None);
        var chargedOk = Assert.IsType<OkObjectResult>(charged.Result);
        var chargedFolio = Assert.IsType<GuestFolioDto>(chargedOk.Value);
        Assert.Equal(12.50m, chargedFolio.Balance);
        Assert.Equal(0, await db.PaymentDetails.CountAsync());
        Assert.Equal(0, await db.GuestFolioItems.CountAsync(row => row.PaymentDetailId != null));

        var items = await controller.ListFolioItems(folio.Id, CancellationToken.None);
        var itemOk = Assert.IsType<OkObjectResult>(items.Result);
        var lines = Assert.IsAssignableFrom<IReadOnlyList<GuestFolioItemDto>>(itemOk.Value);
        Assert.Single(lines);
        Assert.Equal("Frühstück", lines[0].Description);
        Assert.Null(lines[0].PaymentDetailId);

        var closed = await controller.UpdateFolio(
            folio.Id,
            new UpdateGuestFolioRequest { Status = "Closed", Notes = "Abreise" },
            CancellationToken.None);
        var closedFolio = Assert.IsType<GuestFolioDto>(Assert.IsType<OkObjectResult>(closed.Result).Value);
        Assert.False(closedFolio.IsOpen);
        Assert.Equal("Closed", closedFolio.Status);
        Assert.Equal("Abreise", closedFolio.Notes);

        var again = await controller.ChargeFolio(
            folio.Id,
            new ChargeFolioRequest { Description = "Minibar", Amount = 4m },
            CancellationToken.None);
        var conflict = Assert.IsAssignableFrom<ObjectResult>(again.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal(0, await db.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task Pos_CrossTenantFolio_Returns404()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var db = CreateDb(tenantA);
        SeedTenant(db, tenantA);
        db.Tenants.Add(new Tenant
        {
            Id = tenantB,
            Name = "Other",
            Slug = $"b-{tenantB:N}"[..12],
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        await db.SaveChangesAsync();
        var roomId = await SeedRoomAsync(db, tenantB, "901");
        var customerId = await SeedCustomerAsync(db, tenantB, "Fremd");
        var folio = new GuestFolio
        {
            TenantId = tenantB,
            CustomerId = customerId,
            RoomId = roomId,
            CheckIn = DateTime.UtcNow,
            Status = GuestFolioStatus.Open,
            Balance = 0,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.GuestFolios.Add(folio);
        await db.SaveChangesAsync();

        var controller = CreatePosController(db, tenantA);
        var charged = await controller.ChargeFolio(
            folio.Id,
            new ChargeFolioRequest { Description = "Cross", Amount = 1m },
            CancellationToken.None);
        Assert.IsType<NotFoundResult>(charged.Result);
        Assert.IsType<NotFoundResult>((await controller.ListFolioItems(folio.Id, CancellationToken.None)).Result);
        var patched = await controller.UpdateRoomStatus(
            roomId,
            new UpdateRoomStatusRequest { Status = "Cleaning" },
            CancellationToken.None);
        Assert.IsType<NotFoundResult>(patched.Result);
    }

    [Fact]
    public async Task Pos_CreateRoom_ManagerOnly()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        SeedTenant(db, tenantId);
        var cashier = CreatePosController(db, tenantId);
        var denied = await cashier.CreateRoom(
            new CreateRoomRequest { Number = "11", Type = "EZ", Capacity = 1 },
            CancellationToken.None);
        Assert.IsType<ForbidResult>(denied.Result);

        var manager = CreatePosController(db, tenantId, Roles.Manager);
        var created = await manager.CreateRoom(
            new CreateRoomRequest { Number = "11", Type = "EZ", Capacity = 1 },
            CancellationToken.None);
        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ObjectResult>(created.Result).StatusCode);

        var updated = await manager.UpdateRoomStatus(
            Assert.IsType<RoomDto>(Assert.IsType<ObjectResult>(created.Result).Value).Id,
            new UpdateRoomStatusRequest { Status = "Maintenance" },
            CancellationToken.None);
        var room = Assert.IsType<RoomDto>(Assert.IsType<OkObjectResult>(updated.Result).Value);
        Assert.Equal("Maintenance", room.Status);
    }

    [Fact]
    public void Controllers_RequireExpectedPermissions()
    {
        var admin = Assert.Single(
            typeof(AdminLodgingController)
                .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
                .Cast<HasPermissionAttribute>());
        Assert.Equal(AppPermissions.ProductView, admin.Permission);
        var create = Assert.Single(
            typeof(AdminLodgingController)
                .GetMethod(nameof(AdminLodgingController.CreateRoom))!
                .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
                .Cast<HasPermissionAttribute>());
        Assert.Equal(AppPermissions.ProductManage, create.Permission);
        var pos = Assert.Single(
            typeof(PosRoomsController)
                .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
                .Cast<HasPermissionAttribute>());
        Assert.Equal(AppPermissions.CartView, pos.Permission);
    }

    private static AdminLodgingController CreateAdminController(AppDbContext db, Guid tenantId)
    {
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId == Guid.Empty ? null : tenantId);
        return new AdminLodgingController(new LodgingService(db), accessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.Role, Roles.Manager)],
                        authenticationType: "test")),
                },
            },
        };
    }

    private static PosRoomsController CreatePosController(AppDbContext db, Guid tenantId, string? role = null)
    {
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId == Guid.Empty ? null : tenantId);
        return new PosRoomsController(new LodgingService(db), accessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.Role, role ?? Roles.Cashier)],
                        authenticationType: "test")),
                },
            },
        };
    }

    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LodgingApi_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(
            options,
            TenantTestDoubles.TenantAccessorReturning(tenantId == Guid.Empty ? null : tenantId));
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

    private static async Task<Guid> SeedRoomAsync(AppDbContext db, Guid tenantId, string number)
    {
        var room = new Room
        {
            TenantId = tenantId,
            Number = number,
            Type = "DZ",
            Capacity = 2,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return room.Id;
    }

    private static async Task<Guid> SeedCustomerAsync(AppDbContext db, Guid tenantId, string name)
    {
        var customer = new Customer
        {
            TenantId = tenantId,
            Name = name,
            CustomerNumber = $"C-{Guid.NewGuid():N}"[..12],
            Email = $"{Guid.NewGuid():N}@example.test",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer.Id;
    }
}
