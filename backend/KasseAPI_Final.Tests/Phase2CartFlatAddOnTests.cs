using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Phase 2: Adding add-ons as separate cart lines (no CartItemModifiers); legacy cart with modifiers still reads correctly.
/// </summary>
public class Phase2CartFlatAddOnTests
{
    private static void SetAuth(CartController controller, string userId = "u1")
    {
        var identity = new System.Security.Claims.ClaimsIdentity();
        identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId));
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(identity) }
        };
    }

    /// <summary>Risk: Normal product add-item with no SelectedModifiers should succeed and create one CartItem, zero CartItemModifiers.</summary>
    [Fact]
    public async Task AddItem_WithNormalProductAndNoSelectedModifiers_SucceedsAndCreatesOneFlatCartItem()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var cartId = Guid.NewGuid().ToString("N")[..24];

        context.Categories.Add(new Category { Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productId,
            Name = "Döner",
            Price = 6.90m,
            CategoryId = categoryId,
            Category = "Speisen",
            StockQuantity = 10,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            IsActive = true
        });
        context.Carts.Add(new Cart
        {
            CartId = cartId,
            TableNumber = 1,
            UserId = "u1",
            Status = CartStatus.Active,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(2)
        });
        await context.SaveChangesAsync();

        var validation = new NoOpProductModifierValidationService();
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<CartController>();
        var controller = new CartController(context, logger, validation);
        SetAuth(controller);

        var request = new AddItemToCartRequest { ProductId = productId, Quantity = 1, TableNumber = 1 };
        var result = await controller.AddItemToCart(request);

        var ok = result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(ok);
        var countItems = await context.CartItems.CountAsync(ci => ci.CartId == cartId);
        var countModifiers = await context.CartItemModifiers.CountAsync();
        Assert.Equal(1, countItems);
        Assert.Equal(0, countModifiers);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"CartFlatAddOn_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Add-item with IsSellableAddOn product creates one CartItem and zero CartItemModifiers.</summary>
    [Fact]
    public async Task AddItem_WithSellableAddOnProduct_DoesNotCreateCartItemModifiers()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var cartId = Guid.NewGuid().ToString("N")[..24];

        context.Categories.Add(new Category { Id = categoryId, Name = "Extras", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productId,
            Name = "Extra Käse",
            Price = 1.50m,
            CategoryId = categoryId,
            Category = "Extras",
            StockQuantity = 0,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            IsActive = true,
            IsSellableAddOn = true
        });
        context.Carts.Add(new Cart
        {
            CartId = cartId,
            TableNumber = 1,
            UserId = "u1",
            Status = CartStatus.Active,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(2)
        });
        await context.SaveChangesAsync();

        var validation = new NoOpProductModifierValidationService();
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<CartController>();
        var controller = new CartController(context, logger, validation);

        var request = new AddItemToCartRequest
        {
            ProductId = productId,
            Quantity = 2,
            TableNumber = 1
            // No SelectedModifiers
        };

        SetAuth(controller);
        var result = await controller.AddItemToCart(request);

        Assert.NotNull(result);
        var ok = result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(ok);
        // Risk: Add-on must be a separate flat cart line (no embedded modifiers)
        var countItems = await context.CartItems.CountAsync(ci => ci.CartId == cartId);
        var countModifiers = await context.CartItemModifiers.CountAsync();
        Assert.Equal(1, countItems);
        Assert.Equal(0, countModifiers);
        var body = ok!.Value;
        var cartProp = body?.GetType().GetProperty("cart") ?? body?.GetType().GetProperty("Cart");
        var cart = cartProp?.GetValue(body);
        var itemsProp = cart?.GetType().GetProperty("Items");
        var items = itemsProp?.GetValue(cart) as System.Collections.IEnumerable;
        var list = items?.Cast<object>().ToList() ?? new List<object>();
        Assert.Single(list);
        var firstItem = list[0];
        var modsProp = firstItem.GetType().GetProperty("SelectedModifiers");
        var mods = modsProp?.GetValue(firstItem) as System.Collections.IEnumerable;
        Assert.NotNull(mods);
        Assert.Empty(mods.Cast<object>().ToList());
    }

    /// <summary>Phase 3: Add-item request with SelectedModifiers is accepted but no CartItemModifiers are written.</summary>
    [Fact]
    public async Task AddItem_WithSelectedModifiers_Phase3Ignores_DoesNotCreateCartItemModifiers()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();
        var cartId = Guid.NewGuid().ToString("N")[..24];

        context.Categories.Add(new Category { Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productId,
            Name = "Döner",
            Price = 6.90m,
            CategoryId = categoryId,
            Category = "Speisen",
            StockQuantity = 10,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            IsActive = true
        });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Saucen", SortOrder = 0, IsActive = true });
        context.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment { ProductId = productId, ModifierGroupId = groupId, SortOrder = 0 });
        context.Carts.Add(new Cart
        {
            CartId = cartId,
            TableNumber = 1,
            UserId = "u1",
            Status = CartStatus.Active,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(2)
        });
        await context.SaveChangesAsync();

        var validation = new NoOpProductModifierValidationService();
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<CartController>();
        var controller = new CartController(context, logger, validation);
        SetAuth(controller);

        var request = new AddItemToCartRequest
        {
            ProductId = productId,
            Quantity = 1,
            TableNumber = 1,
            SelectedModifiers = new List<SelectedModifierInputDto> { new() { Id = modifierId, Quantity = 1 } }
        };

        var result = await controller.AddItemToCart(request);

        var ok = result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(ok);
        var countItems = await context.CartItems.CountAsync(ci => ci.CartId == cartId);
        var countModifiers = await context.CartItemModifiers.CountAsync();
        Assert.Equal(1, countItems);
        Assert.Equal(0, countModifiers);
    }

    /// <summary>Cart with legacy CartItemModifiers still loads and does not crash when building response.</summary>
    [Fact]
    public async Task GetCart_WithLegacyCartItemModifiers_LoadsWithoutCrash()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();
        var cartId = Guid.NewGuid().ToString("N")[..24];
        var cartItemId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productId,
            Name = "Döner",
            Price = 6.90m,
            CategoryId = categoryId,
            Category = "Speisen",
            StockQuantity = 10,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            IsActive = true
        });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Saucen", SortOrder = 0, IsActive = true });
        context.Carts.Add(new Cart
        {
            CartId = cartId,
            TableNumber = 1,
            UserId = "u1",
            Status = CartStatus.Active,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(2)
        });
        context.CartItems.Add(new CartItem
        {
            Id = cartItemId,
            CartId = cartId,
            ProductId = productId,
            Quantity = 1,
            UnitPrice = 6.90m
        });
        context.CartItemModifiers.Add(new CartItemModifier
        {
            CartItemId = cartItemId,
            ModifierId = modifierId,
            Name = "Ketchup",
            Price = 0.30m,
            Quantity = 1
        });
        await context.SaveChangesAsync();

        var validation = new NoOpProductModifierValidationService();
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<CartController>();
        var controller = new CartController(context, logger, validation);

        SetAuth(controller);
        var result = await controller.GetCart(cartId);

        Assert.NotNull(result);
        var ok = result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(ok);
        // Risk: Legacy cart with CartItemModifiers must load and serialize SelectedModifiers in response
        var cartResponse = ok!.Value as KasseAPI_Final.Controllers.CartResponse;
        Assert.NotNull(cartResponse);
        Assert.Single(cartResponse.Items);
        var item = cartResponse.Items[0];
        Assert.NotNull(item.SelectedModifiers);
        Assert.Single(item.SelectedModifiers);
        Assert.Equal("Ketchup", item.SelectedModifiers[0].Name);
        Assert.Equal(modifierId, item.SelectedModifiers[0].Id);
    }
}
