using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class PosSplitSessionServiceTests
{
    private static readonly Guid TenantId = LegacyDefaultTenantIds.Primary;
    private const string CashierId = "cashier-split-test";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PosSplit_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(TenantId));
    }

    private static PosSplitSessionService CreateService(AppDbContext ctx, IPosCartTableOpsService? cartOps = null)
    {
        return new PosSplitSessionService(
            ctx,
            TenantTestDoubles.PrimaryTenantResolver,
            cartOps ?? Mock.Of<IPosCartTableOpsService>(),
            NullLogger<PosSplitSessionService>.Instance);
    }

    [Fact]
    public async Task StartSplit_CopiesAllCartLines()
    {
        await using var ctx = CreateContext();
        var productId = Guid.NewGuid();
        ctx.Products.Add(new Product
        {
            Id = productId,
            TenantId = TenantId,
            Name = "Pizza",
            Price = 12m,
            TaxType = 1,
            CategoryId = Guid.NewGuid(),
        });

        var cart = new Cart
        {
            CartId = Guid.NewGuid().ToString(),
            TableNumber = 3,
            UserId = CashierId,
            Status = CartStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddHours(2),
        };
        cart.Items.Add(new CartItem
        {
            CartId = cart.CartId,
            ProductId = productId,
            Quantity = 2,
            UnitPrice = 12m,
        });
        ctx.Carts.Add(cart);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var session = await svc.StartSplitAsync(CashierId, new StartSplitRequest { CartId = cart.Id });

        Assert.Single(session.Items);
        Assert.Equal(24m, session.GrandTotal);
        Assert.False(session.IsCompleted);
        Assert.Equal(string.Empty, session.Items[0].CustomerName);
    }

    [Fact]
    public async Task CompleteSplit_CreatesCartsPerCustomerGroup()
    {
        await using var ctx = CreateContext();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        ctx.Products.AddRange(
            new Product { Id = productA, TenantId = TenantId, Name = "A", Price = 10m, TaxType = 1, CategoryId = Guid.NewGuid() },
            new Product { Id = productB, TenantId = TenantId, Name = "B", Price = 5m, TaxType = 1, CategoryId = Guid.NewGuid() });

        var cart = new Cart
        {
            CartId = Guid.NewGuid().ToString(),
            TableNumber = 1,
            UserId = CashierId,
            Status = CartStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddHours(2),
        };
        cart.Items.Add(new CartItem { CartId = cart.CartId, ProductId = productA, Quantity = 1, UnitPrice = 10m });
        cart.Items.Add(new CartItem { CartId = cart.CartId, ProductId = productB, Quantity = 1, UnitPrice = 5m });
        ctx.Carts.Add(cart);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var session = await svc.StartSplitAsync(CashierId, new StartSplitRequest { CartId = cart.Id });

        foreach (var (item, name, seat) in new[]
        {
            (session.Items[0].Id, "Gast A", 1),
            (session.Items[1].Id, "Gast B", 2),
        })
        {
            await svc.AssignItemAsync(CashierId, session.Id, new AssignItemRequest
            {
                ItemId = item,
                CustomerName = name,
                SeatNumber = seat,
            });
        }

        var newCartIds = await svc.CompleteSplitAsync(CashierId, session.Id);
        Assert.Equal(2, newCartIds.Count);

        var originalItems = await ctx.CartItems.Where(i => i.CartId == cart.CartId).CountAsync();
        Assert.Equal(0, originalItems);

        var completed = await ctx.SplitSessions.AsNoTracking().SingleAsync(s => s.Id == session.Id);
        Assert.True(completed.IsCompleted);
    }
}
