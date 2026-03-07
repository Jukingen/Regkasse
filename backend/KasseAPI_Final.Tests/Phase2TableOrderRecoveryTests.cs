using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Phase 2: Table order recovery still loads and serializes legacy TableOrderItemModifiers as SelectedModifiers.
/// </summary>
public class Phase2TableOrderRecoveryTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"TableOrderRecovery_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static void SetAuth(CartController controller, string userId = "u1")
    {
        var identity = new System.Security.Claims.ClaimsIdentity();
        identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId));
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(identity) }
        };
    }

    /// <summary>Risk: Table orders with embedded TableOrderItemModifiers must still load and serialize SelectedModifiers in recovery response.</summary>
    [Fact]
    public async Task GetTableOrdersForRecovery_WithLegacyTableOrderItemModifiers_SerializesSelectedModifiers()
    {
        await using var context = CreateContext();
        var toId = "TO-1-20260307-abc12345";
        var toItemId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();

        // User required for Include(to => to.User) in GetTableOrdersForRecovery
        context.Users.Add(new ApplicationUser
        {
            Id = "u1",
            UserName = "testuser",
            NormalizedUserName = "TESTUSER",
            Email = "t@t.com",
            NormalizedEmail = "T@T.COM",
            FirstName = "Test",
            LastName = "User",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        });
        context.TableOrders.Add(new TableOrder
        {
            TableOrderId = toId,
            TableNumber = 1,
            UserId = "u1",
            Status = TableOrderStatus.Active,
            WaiterName = "Test",
            Subtotal = 6.50m,
            TaxAmount = 0.65m,
            TotalAmount = 7.20m,
            OrderStartTime = DateTime.UtcNow,
            LastModifiedTime = DateTime.UtcNow
        });
        context.TableOrderItems.Add(new TableOrderItem
        {
            Id = toItemId,
            TableOrderId = toId,
            ProductId = productId,
            ProductName = "Döner",
            Quantity = 1,
            UnitPrice = 6.90m,
            TotalPrice = 6.90m,
            TaxType = 2,
            TaxRate = 10m
        });
        context.TableOrderItemModifiers.Add(new TableOrderItemModifier
        {
            TableOrderItemId = toItemId,
            ModifierId = modifierId,
            Name = "Ketchup",
            Price = 0.30m,
            Quantity = 1
        });
        await context.SaveChangesAsync();

        var validation = new ProductModifierValidationService(context);
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<CartController>();
        var controller = new CartController(context, logger, validation);
        SetAuth(controller);

        var result = await controller.GetTableOrdersForRecovery();

        var ok = result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(ok);
        var tableOrdersProp = ok!.Value?.GetType().GetProperty("tableOrders");
        var tableOrders = tableOrdersProp?.GetValue(ok.Value) as System.Collections.IEnumerable;
        Assert.NotNull(tableOrders);
        var list = tableOrders.Cast<object>().ToList();
        Assert.NotEmpty(list);
        var first = list[0];
        var itemsProp = first.GetType().GetProperty("Items");
        var items = itemsProp?.GetValue(first) as System.Collections.IEnumerable;
        Assert.NotNull(items);
        var itemsList = items.Cast<object>().ToList();
        Assert.Single(itemsList);
        var item = itemsList[0];
        var selectedModifiersProp = item.GetType().GetProperty("SelectedModifiers");
        var selectedModifiers = selectedModifiersProp?.GetValue(item) as System.Collections.IEnumerable;
        Assert.NotNull(selectedModifiers);
        var modsList = selectedModifiers.Cast<object>().ToList();
        Assert.Single(modsList);
        var nameProp = modsList[0].GetType().GetProperty("Name");
        var name = nameProp?.GetValue(modsList[0]);
        Assert.Equal("Ketchup", name);
    }
}
