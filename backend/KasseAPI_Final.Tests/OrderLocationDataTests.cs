using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OrderLocationDataTests
{
    [Fact]
    public async Task CreateOrder_PersistsLocationData()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var productId = Guid.NewGuid();
        db.Products.Add(new Product
        {
            Id = productId,
            TenantId = tenantId,
            Name = "Hausbesuch",
            Category = "Service",
            Price = 40m,
            TaxType = 1,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var controller = new OrdersController(
            db,
            Mock.Of<ILogger<OrdersController>>(),
            TenantTestDoubles.SettingsResolverReturning(tenantId));

        var result = await controller.CreateOrder(new CreateOrderRequest
        {
            CustomerName = "Mobil Kunde",
            Notes = "Vormittag",
            LocationData = new OrderLocationData
            {
                Street = "Seestraße 9",
                PostalCode = "5020",
                City = "Salzburg",
                Notes = "Klingel 2",
            },
            Items = [new OrderItemRequest { ProductId = productId, Quantity = 1 }],
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var order = Assert.IsType<Order>(created.Value);
        Assert.Equal("Seestraße 9", order.LocationData?.Street);
        var saved = await db.Orders.SingleAsync(row => row.Id == order.Id);
        Assert.Equal("5020", saved.LocationData?.PostalCode);
        Assert.Equal("Klingel 2", saved.LocationData?.Notes);
    }

    [Fact]
    public async Task Model_PersistsEmptyLocationAsNull()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        db.Orders.Add(new Order
        {
            OrderId = "ORD-TEST-1",
            TenantId = tenantId,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            LocationData = new OrderLocationData(),
        });
        await db.SaveChangesAsync();

        var saved = await db.Orders.SingleAsync();
        Assert.NotNull(saved.LocationData);
        Assert.Null(saved.LocationData!.Street);
    }

    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"OrderLocation_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }
}
