using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OrdersListPaginationTests
{
    [Fact]
    public async Task GetOrders_Paginates_AndDoesNotReturnEveryRow()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"OrdersPage_{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
        var now = DateTime.UtcNow;
        db.Orders.AddRange(
            NewOrder("ORD-OLD", now.AddHours(-3)),
            NewOrder("ORD-MID", now.AddHours(-2)),
            NewOrder("ORD-NEW", now.AddHours(-1)));
        await db.SaveChangesAsync();

        var controller = new OrdersController(
            db,
            Mock.Of<ILogger<OrdersController>>(),
            TenantTestDoubles.SettingsResolverReturning(tenantId));

        var first = await ReadPage(await controller.GetOrders(page: 1, pageSize: 2));
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(2, first.Items.Count);
        Assert.Equal(["ORD-NEW", "ORD-MID"], first.Items);

        var second = await ReadPage(await controller.GetOrders(page: 2, pageSize: 2));
        Assert.Equal(3, second.TotalCount);
        Assert.Equal(["ORD-OLD"], second.Items);
    }

    private static async Task<(int TotalCount, List<string> Items)> ReadPage(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(
            ok.Value,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
            });
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var names = new List<string>();
        foreach (var item in root.GetProperty("items").EnumerateArray())
            names.Add(item.GetProperty("orderId").GetString()!);

        return (root.GetProperty("totalCount").GetInt32(), names);
    }

    private static Order NewOrder(string orderId, DateTime orderDate) => new()
    {
        OrderId = orderId,
        OrderDate = orderDate,
        Status = OrderStatus.Pending,
        Subtotal = 10m,
        TaxAmount = 2m,
        DiscountAmount = 0m,
        TotalAmount = 12m,
    };
}
