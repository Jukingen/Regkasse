using System.Runtime.CompilerServices;
using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tickets;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
        var events = new TicketEventService(db);
        var created = await events.CreateAsync(tenantId, SampleEvent("Kammerkonzert"), CancellationToken.None);
        Assert.NotNull(created);
        var seat = await events.AddSeatAsync(
            created!.Id,
            new CreateTicketSeatRequest { Row = "A", Number = "12", Section = "Parkett" },
            CancellationToken.None);
        var added = Assert.IsType<TicketSeatActionResult.OkSeat>(seat);

        var reserved = await events.ReserveAsync(created.Id, added.Seat.Id, CancellationToken.None);
        Assert.IsType<TicketSeatActionResult.OkSeat>(reserved);

        var sold = await events.SellAsync(tenantId, created.Id, added.Seat.Id, CancellationToken.None);
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
        var events = new TicketEventService(db);
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
        var events = new TicketEventService(db);
        var created = await events.CreateAsync(owner, SampleEvent("Eigen"), CancellationToken.None);

        var controller = CreateAdminController(db, other);
        var result = await controller.Update(created!.Id, SampleEvent("Geändert"), CancellationToken.None);
        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Equal("Eigen", (await db.TicketEvents.IgnoreQueryFilters().SingleAsync()).Name);
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
        return new PosTicketEventsController(new TicketEventService(db), accessor, new PermissiveVerticalProfileGuard())
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
        return new AdminTicketEventsController(new TicketEventService(db), accessor)
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
