using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Limits;
using KasseAPI_Final.Services.Offline;
using KasseAPI_Final.Services.Pricing;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Controller + database checks for the tenant-isolation hardening slice.
/// In-memory EF; no Docker. Not Category=PostgreSql.
/// </summary>
public sealed class CriticalHardeningIntegrationTests
{
    [Fact]
    public async Task OfflineQueue_Manager_ForeignTenantId_Returns404()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var registerA = Guid.NewGuid();
        var registerB = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var db = CreateContext(tenantA);
        TenantTestDoubles.EnsureTenant(db, tenantA, "oa" + tenantA.ToString("N")[..8]);
        TenantTestDoubles.EnsureTenant(db, tenantB, "ob" + tenantB.ToString("N")[..8]);
        db.CashRegisters.Add(Register(tenantA, registerA, "A-01", now));
        db.CashRegisters.Add(Register(tenantB, registerB, "B-01", now));
        db.OfflineTransactions.Add(Queued(tenantA, registerA, now, 11m));
        db.OfflineTransactions.Add(Queued(tenantB, registerB, now, 99m));
        await db.SaveChangesAsync();

        var controller = CreateQueueController(db, tenantA, Roles.Manager);
        var foreign = await controller.ListQueued(tenantB);

        Assert.IsType<NotFoundResult>(foreign.Result);

        var own = await controller.ListQueued(tenantId: null);
        var ok = Assert.IsType<OkObjectResult>(own.Result);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<TseOfflineQueuedTransactionDto>>(ok.Value);
        var row = Assert.Single(rows);
        Assert.Equal(registerA, row.CashRegisterId);
        Assert.Equal(11m, row.Amount);
        Assert.DoesNotContain(rows, r => r.CashRegisterId == registerB);
    }

    [Fact]
    public async Task ForceCleanup_WithoutTable_DoesNotTouchOtherTenantCarts()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userA = "clean-a-" + Guid.NewGuid().ToString("N");
        var userB = "clean-b-" + Guid.NewGuid().ToString("N");
        var cartA = "ca-" + Guid.NewGuid().ToString("N");
        var cartB = "cb-" + Guid.NewGuid().ToString("N");

        await using var db = CreateContext(tenantA);
        TenantTestDoubles.EnsureTenant(db, tenantA, "ca" + tenantA.ToString("N")[..8]);
        TenantTestDoubles.EnsureTenant(db, tenantB, "cb" + tenantB.ToString("N")[..8]);
        db.Users.Add(TestUser(userA));
        db.Users.Add(TestUser(userB));
        db.UserTenantMemberships.Add(new UserTenantMembership { UserId = userA, TenantId = tenantA, IsActive = true });
        db.UserTenantMemberships.Add(new UserTenantMembership { UserId = userB, TenantId = tenantB, IsActive = true });
        db.Carts.Add(OpenCart(cartA, userA));
        db.Carts.Add(OpenCart(cartB, userB));
        await db.SaveChangesAsync();

        var controller = new CartController(
            db,
            NullLogger<CartController>.Instance,
            new NoOpProductModifierValidationService(),
            new PricingRuleResolver(db, TenantTestDoubles.SettingsResolverReturning(tenantA)),
            TenantTestDoubles.SettingsResolverReturning(tenantA),
            Mock.Of<IPosCartTableOpsService>());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userA),
                        new Claim(ClaimTypes.Role, Roles.Cashier)
                    },
                    "Test"))
            }
        };

        var result = await controller.ForceCartCleanup(new ForceCleanupRequest { Reason = "shift end" });
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ForceCleanupResponse>(ok.Value);
        var cleaned = Assert.Single(body.CleanedCarts);
        Assert.Equal(cartA, cleaned.CartId);
        Assert.Equal(userA, cleaned.UserId);
        Assert.DoesNotContain(body.CleanedCarts, c => c.UserId == userB || c.CartId == cartB);

        Assert.False(await db.Carts.AnyAsync(c => c.CartId == cartA));
        Assert.True(await db.Carts.AnyAsync(c => c.CartId == cartB && c.UserId == userB));
    }

    private static AdminTseOfflineQueueController CreateQueueController(AppDbContext db, Guid ambientTenantId, string role)
    {
        var limits = new Mock<ITenantLimitService>();
        limits
            .Setup(s => s.GetLimitValueAsync(It.IsAny<Guid>(), TenantLimitKeys.MaxOfflineTransactions, It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);
        var monitoring = new Mock<IOptionsMonitor<OfflineMonitoringOptions>>();
        monitoring.Setup(o => o.CurrentValue).Returns(new OfflineMonitoringOptions());
        var queue = new TseOfflineQueueService(
            db,
            Mock.Of<IActivityEventPublisher>(),
            Mock.Of<IAuditLogService>(),
            limits.Object,
            monitoring.Object,
            NullLogger<TseOfflineQueueService>.Instance);
        var controller = new AdminTseOfflineQueueController(queue, TenantTestDoubles.SettingsResolverReturning(ambientTenantId));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "manager-1"),
                        new Claim(ClaimTypes.Role, role)
                    },
                    "Test"))
            }
        };
        return controller;
    }

    private static AppDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("Hardening_" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static CashRegister Register(Guid tenantId, Guid id, string number, DateTime now) => new()
    {
        TenantId = tenantId,
        Id = id,
        RegisterNumber = number,
        Location = "Test",
        StartingBalance = 0,
        CurrentBalance = 0,
        LastBalanceUpdate = now,
        Status = RegisterStatus.Open,
        CreatedAt = now,
        IsActive = true
    };

    private static OfflineTransaction Queued(Guid tenantId, Guid registerId, DateTime receivedAt, decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        CashRegisterId = registerId,
        PayloadJson = "{\"totalAmount\":" + amount.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"payment\":{\"method\":\"cash\"}}",
        ServerReceivedAtUtc = receivedAt,
        OfflineCreatedAtUtc = receivedAt,
        Status = OfflineTransactionStatus.NonFiscalPending,
        CreatedAt = receivedAt,
        IsActive = true
    };

    private static ApplicationUser TestUser(string userId) => new()
    {
        Id = userId,
        UserName = userId,
        NormalizedUserName = userId.ToUpperInvariant(),
        FirstName = "Test",
        LastName = "User",
        IsActive = true
    };

    private static Cart OpenCart(string cartId, string userId) => new()
    {
        CartId = cartId,
        UserId = userId,
        TableNumber = 3,
        Status = CartStatus.Active,
        ExpiresAt = DateTime.UtcNow.AddHours(5),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IsActive = true
    };
}
