using System.Runtime.CompilerServices;
using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tickets;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TicketEventTests
{
    private static readonly ConditionalWeakTable<AppDbContext, ICurrentTenantAccessor> Accessors = new();

    [Fact]
    public async Task CreateEventAndSeat_ThenSell_StoresRedemptionWithEventAndSeat()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();
        var events = CreateEvents(db);
        var created = await events.CreateAsync(tenantId, SampleEvent("Kammerkonzert"), CancellationToken.None);
        Assert.NotNull(created);
        var seat = await events.AddSeatAsync(
            created!.Id,
            new CreateTicketSeatRequest { Row = "A", Number = "12", Section = "Parkett" },
            CancellationToken.None);
        var added = Assert.IsType<TicketSeatActionResult.OkSeat>(seat);

        var reserved = await events.ReserveAsync(created.Id, added.Seat.Id, "cashier-a", CancellationToken.None);
        Assert.IsType<TicketSeatActionResult.OkSeat>(reserved);

        var sold = await events.SellAsync(tenantId, created.Id, added.Seat.Id, "cashier-a", CancellationToken.None);
        var sale = Assert.IsType<TicketSeatActionResult.OkSale>(sold);
        Assert.StartsWith("TKT-", sale.Ticket.Code, StringComparison.Ordinal);
        Assert.Equal("Kammerkonzert", sale.Ticket.EventName);
        Assert.Equal("Parkett A-12", sale.Ticket.SeatLabel);

        var redemption = await db.TicketRedemptions.SingleAsync();
        Assert.Equal(created.Id, redemption.EventId);
        Assert.Equal(added.Seat.Id, redemption.SeatId);
        Assert.Equal(TicketCodeHasher.DisplayPrefix(TicketCodeHasher.HashRaw(sale.Ticket.Code)), redemption.TicketCode);
        Assert.Null(redemption.PaymentDetailId);
    }

    [Fact]
    public async Task PosSeatMap_CrossTenant_Returns404()
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        await using var db = await CreateDbAsync(owner);
        SeedTenant(db, owner);
        SeedTenant(db, other);
        await db.SaveChangesAsync();
        var events = CreateEvents(db);
        var created = await events.CreateAsync(owner, SampleEvent("Fremd"), CancellationToken.None);

        var controller = CreatePosController(db, other);
        var result = await controller.Seats(created!.Id, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task AdminUpdate_CrossTenant_Returns404()
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        await using var db = await CreateDbAsync(owner);
        SeedTenant(db, owner);
        SeedTenant(db, other);
        await db.SaveChangesAsync();
        var events = CreateEvents(db);
        var created = await events.CreateAsync(owner, SampleEvent("Eigen"), CancellationToken.None);

        var controller = CreateAdminController(db, other);
        var result = await controller.Update(created!.Id, SampleEvent("Geändert"), CancellationToken.None);
        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Equal("Eigen", (await db.TicketEvents.IgnoreQueryFilters().SingleAsync()).Name);
    }

    [Fact]
    public async Task Sell_ActiveHold_BlocksOtherCashier_AndOwnerWritesTicketSeatSoldAudit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();
        var audit = TicketAuditTestDoubles.Loose();
        var events = CreateEvents(db, audit);
        var created = await events.CreateAsync(tenantId, SampleEvent("Hold"), CancellationToken.None);
        var seat = Assert.IsType<TicketSeatActionResult.OkSeat>(
            await events.AddSeatAsync(created!.Id, new CreateTicketSeatRequest { Row = "A", Number = "1" }, CancellationToken.None));
        Assert.IsType<TicketSeatActionResult.OkSeat>(
            await events.ReserveAsync(created.Id, seat.Seat.Id, "cashier-a", CancellationToken.None));

        var blocked = await events.SellAsync(tenantId, created.Id, seat.Seat.Id, "cashier-b", CancellationToken.None);
        var error = Assert.IsType<TicketSeatActionResult.Error>(blocked);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal(TicketSeatErrorCodes.SeatTaken, error.Code);

        var sold = await events.SellAsync(tenantId, created.Id, seat.Seat.Id, "cashier-a", CancellationToken.None);
        Assert.IsType<TicketSeatActionResult.OkSale>(sold);
        audit.Verify(a => a.LogSystemOperationAsync(
            "TICKET_SEAT_SOLD",
            "TicketSeat",
            "cashier-a",
            "Cashier",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            AuditLogStatus.Success,
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>(),
            It.IsAny<ImpersonationAuditContext.Snapshot?>(),
            AuditEventType.TicketSeatSold,
            seat.Seat.Id,
            tenantId,
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>()), Times.Once);
        Assert.Equal(147, (int)AuditEventType.TicketSeatSold);
    }

    [Fact]
    public async Task Sell_ExpiredHold_SucceedsForAnotherCashier_AndClearsHold()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();
        var events = CreateEvents(db);
        var created = await events.CreateAsync(tenantId, SampleEvent("Expired"), CancellationToken.None);
        var seat = Assert.IsType<TicketSeatActionResult.OkSeat>(
            await events.AddSeatAsync(created!.Id, new CreateTicketSeatRequest { Row = "B", Number = "2" }, CancellationToken.None));
        Assert.IsType<TicketSeatActionResult.OkSeat>(
            await events.ReserveAsync(created.Id, seat.Seat.Id, "cashier-a", CancellationToken.None));
        var stored = await db.TicketSeats.SingleAsync();
        stored.ReservedUntilUtc = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var sold = await events.SellAsync(tenantId, created.Id, seat.Seat.Id, "cashier-b", CancellationToken.None);
        Assert.IsType<TicketSeatActionResult.OkSale>(sold);
        var after = await db.TicketSeats.SingleAsync();
        Assert.Equal(TicketSeatStatus.Sold, after.Status);
        Assert.Null(after.ReservedUntilUtc);
        Assert.Null(after.ReservedByUserId);
    }

    [Fact]
    public async Task AddSeat_OverCategoryCapacity_Returns409()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();
        var events = CreateEvents(db);
        var created = await events.CreateAsync(tenantId, SampleEvent("Cap"), CancellationToken.None);
        var category = Assert.IsType<TicketCatalogResult.CategoryOk>(
            await events.CreateCategoryAsync(
                tenantId,
                new UpsertTicketPriceCategoryRequest { Code = "vip", Name = "VIP" },
                "manager-1",
                CancellationToken.None));
        Assert.IsType<TicketCatalogResult.PriceOk>(
            await events.SetCategoryPriceAsync(
                created!.Id,
                new SetTicketEventCategoryPriceRequest
                {
                    CategoryId = category.Category.Id,
                    Price = 40m,
                    Capacity = 1,
                },
                "manager-1",
                CancellationToken.None));

        var first = await events.AddSeatAsync(
            created.Id,
            new CreateTicketSeatRequest { Row = "A", Number = "1", CategoryId = category.Category.Id, SeatRole = 0 },
            CancellationToken.None);
        Assert.IsType<TicketSeatActionResult.OkSeat>(first);
        var second = await events.AddSeatAsync(
            created.Id,
            new CreateTicketSeatRequest { Row = "A", Number = "2", CategoryId = category.Category.Id },
            CancellationToken.None);
        var error = Assert.IsType<TicketSeatActionResult.Error>(second);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal(TicketSeatErrorCodes.CategoryCapacityReached, error.Code);
    }

    [Fact]
    public async Task Sell_CompanionBeforeTarget_Returns409_ThenSucceedsAfterTargetIsSold()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();
        var events = CreateEvents(db);
        var created = await events.CreateAsync(tenantId, SampleEvent("Paar"), CancellationToken.None);
        var accessible = Assert.IsType<TicketSeatActionResult.OkSeat>(
            await events.AddSeatAsync(
                created!.Id,
                new CreateTicketSeatRequest { Row = "A", Number = "3", SeatRole = (int)TicketSeatRole.Accessible },
                CancellationToken.None));
        var companion = Assert.IsType<TicketSeatActionResult.OkSeat>(
            await events.AddSeatAsync(
                created.Id,
                new CreateTicketSeatRequest
                {
                    Row = "A",
                    Number = "4",
                    SeatRole = (int)TicketSeatRole.Companion,
                    CompanionOfSeatId = accessible.Seat.Id,
                },
                CancellationToken.None));
        Assert.Equal(accessible.Seat.Id, companion.Seat.CompanionOfSeatId);

        var blocked = await events.SellAsync(tenantId, created.Id, companion.Seat.Id, "cashier-a", CancellationToken.None);
        var error = Assert.IsType<TicketSeatActionResult.Error>(blocked);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal(TicketSeatErrorCodes.CompanionTargetNotSold, error.Code);
        Assert.Empty(await db.TicketRedemptions.ToListAsync());

        Assert.IsType<TicketSeatActionResult.OkSale>(
            await events.SellAsync(tenantId, created.Id, accessible.Seat.Id, "cashier-a", CancellationToken.None));
        Assert.IsType<TicketSeatActionResult.OkSale>(
            await events.SellAsync(tenantId, created.Id, companion.Seat.Id, "cashier-a", CancellationToken.None));
        Assert.Equal(2, await db.TicketRedemptions.CountAsync());
    }

    [Fact]
    public async Task Sell_UnpairedCompanion_Succeeds()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();
        var events = CreateEvents(db);
        var created = await events.CreateAsync(tenantId, SampleEvent("Frei"), CancellationToken.None);
        var companion = Assert.IsType<TicketSeatActionResult.OkSeat>(
            await events.AddSeatAsync(
                created!.Id,
                new CreateTicketSeatRequest { Row = "B", Number = "1", SeatRole = (int)TicketSeatRole.Companion },
                CancellationToken.None));
        Assert.Null(companion.Seat.CompanionOfSeatId);

        var sold = await events.SellAsync(tenantId, created.Id, companion.Seat.Id, "cashier-a", CancellationToken.None);
        Assert.IsType<TicketSeatActionResult.OkSale>(sold);
    }

    [Fact]
    public async Task AddSeat_CompanionTargetNotAccessible_Returns400()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();
        var events = CreateEvents(db);
        var created = await events.CreateAsync(tenantId, SampleEvent("Ziel"), CancellationToken.None);
        var standard = Assert.IsType<TicketSeatActionResult.OkSeat>(
            await events.AddSeatAsync(
                created!.Id,
                new CreateTicketSeatRequest { Row = "A", Number = "1", SeatRole = (int)TicketSeatRole.Standard },
                CancellationToken.None));

        var blocked = await events.AddSeatAsync(
            created.Id,
            new CreateTicketSeatRequest
            {
                Row = "A",
                Number = "2",
                SeatRole = (int)TicketSeatRole.Companion,
                CompanionOfSeatId = standard.Seat.Id,
            },
            CancellationToken.None);
        var error = Assert.IsType<TicketSeatActionResult.Error>(blocked);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal(TicketSeatErrorCodes.CompanionTargetInvalid, error.Code);
        Assert.Equal(1, await db.TicketSeats.CountAsync());
    }

    [Fact]
    public async Task Sell_ActiveHoldOnCompanion_ReturnsSeatTakenBeforePairingRule()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();
        var events = CreateEvents(db);
        var created = await events.CreateAsync(tenantId, SampleEvent("HoldPair"), CancellationToken.None);
        var accessible = Assert.IsType<TicketSeatActionResult.OkSeat>(
            await events.AddSeatAsync(
                created!.Id,
                new CreateTicketSeatRequest { Row = "A", Number = "1", SeatRole = (int)TicketSeatRole.Accessible },
                CancellationToken.None));
        var companion = Assert.IsType<TicketSeatActionResult.OkSeat>(
            await events.AddSeatAsync(
                created.Id,
                new CreateTicketSeatRequest
                {
                    Row = "A",
                    Number = "2",
                    SeatRole = (int)TicketSeatRole.Companion,
                    CompanionOfSeatId = accessible.Seat.Id,
                },
                CancellationToken.None));
        Assert.IsType<TicketSeatActionResult.OkSeat>(
            await events.ReserveAsync(created.Id, companion.Seat.Id, "cashier-a", CancellationToken.None));

        var blocked = await events.SellAsync(tenantId, created.Id, companion.Seat.Id, "cashier-b", CancellationToken.None);
        var error = Assert.IsType<TicketSeatActionResult.Error>(blocked);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal(TicketSeatErrorCodes.SeatTaken, error.Code);
    }

    private static UpsertTicketEventRequest SampleEvent(string name) => new()
    {
        Name = name,
        Venue = "Saal 1",
        Capacity = 20,
        IsActive = true,
        StartAtUtc = new DateTime(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc),
        EndAtUtc = new DateTime(2026, 11, 1, 20, 0, 0, DateTimeKind.Utc),
    };

    private static async Task<AppDbContext> CreateDbAsync(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TicketEvents_{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var db = new AppDbContext(options, accessor);
        Accessors.Add(db, accessor);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static PosTicketEventsController CreatePosController(AppDbContext db, Guid tenantId)
    {
        var accessor = Accessors.GetValue(db, _ => throw new InvalidOperationException("missing accessor"));
        if (accessor is TenantTestDoubles.MutableTenantAccessor mutable)
            mutable.TenantId = tenantId;
        return new PosTicketEventsController(CreateEvents(db), accessor, new PermissiveVerticalProfileGuard())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "cashier"), new Claim(ClaimTypes.Role, Roles.Cashier)],
                        "test")),
                },
            },
        };
    }

    private static AdminTicketEventsController CreateAdminController(AppDbContext db, Guid tenantId)
    {
        var accessor = Accessors.GetValue(db, _ => throw new InvalidOperationException("missing accessor"));
        if (accessor is TenantTestDoubles.MutableTenantAccessor mutable)
            mutable.TenantId = tenantId;
        return new AdminTicketEventsController(CreateEvents(db), accessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.Role, Roles.Manager)],
                        "test")),
                },
            },
        };
    }

    private static TicketEventService CreateEvents(AppDbContext db, Mock<IAuditLogService>? audit = null) =>
        new(db, (audit ?? TicketAuditTestDoubles.Loose()).Object, NullLogger<TicketEventService>.Instance);

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
    }
}
