using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OnlineOrderModelTests
{
    [Fact]
    public async Task OnlineOrder_persists_items_and_modifiers_with_cascade()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(OnlineOrder_persists_items_and_modifiers_with_cascade) + Guid.NewGuid())
            .Options;

        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Online Cafe",
            Slug = "online-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        var orderId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        db.OnlineOrders.Add(new OnlineOrder
        {
            Id = orderId,
            TenantId = tenantId,
            OrderNumber = "ORD-001",
            CustomerName = "Max Mustermann",
            CustomerPhone = "+431234567",
            CustomerEmail = "max@example.com",
            OrderType = OnlineOrderTypes.Delivery,
            DeliveryAddress = "Hauptstrasse 1",
            Subtotal = 10m,
            Tax = 2m,
            Total = 12m,
            PaymentMethod = OnlineOrderPaymentMethods.Online,
            PaymentStatus = OnlineOrderPaymentStatuses.Pending,
            OrderStatus = OnlineOrderStatuses.Pending,
            Source = OnlineOrderSources.Web,
            Items =
            [
                new OnlineOrderItem
                {
                    Id = itemId,
                    OnlineOrderId = orderId,
                    ProductId = Guid.NewGuid(),
                    ProductName = "Espresso",
                    Quantity = 2,
                    Price = 5m,
                    Total = 10m,
                    Modifiers =
                    [
                        new OnlineOrderItemModifier
                        {
                            Id = Guid.NewGuid(),
                            OnlineOrderItemId = itemId,
                            Name = "Extra shot",
                            Price = 0.5m,
                            Quantity = 1
                        }
                    ]
                }
            ]
        });

        await db.SaveChangesAsync();

        var loaded = await db.OnlineOrders
            .IgnoreQueryFilters()
            .Include(o => o.Items)
            .ThenInclude(i => i.Modifiers)
            .SingleAsync(o => o.Id == orderId);

        Assert.Equal("ORD-001", loaded.OrderNumber);
        Assert.Equal(OnlineOrderTypes.Delivery, loaded.OrderType);
        Assert.Single(loaded.Items);
        Assert.Single(loaded.Items.First().Modifiers);
        Assert.Equal("Extra shot", loaded.Items.First().Modifiers.First().Name);
    }

    [Fact]
    public void Status_constants_match_sketch_values()
    {
        Assert.Contains(OnlineOrderStatuses.Pending, OnlineOrderStatuses.All);
        Assert.Contains(OnlineOrderStatuses.Accepted, OnlineOrderStatuses.All);
        Assert.Contains(OnlineOrderPaymentMethods.Cash, OnlineOrderPaymentMethods.All);
        Assert.Contains(OnlineOrderTypes.DineIn, OnlineOrderTypes.All);
        Assert.Contains(OnlineOrderSources.Pwa, OnlineOrderSources.All);
    }
}
