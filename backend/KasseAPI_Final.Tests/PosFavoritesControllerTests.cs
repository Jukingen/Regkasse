using System.Security.Claims;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public class PosFavoritesControllerTests
{
    private static readonly Guid TenantId = LegacyDefaultTenantIds.Primary;
    private const string CashierId = "cashier-fav-test";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PosFavoritesCtrl_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(TenantId));
    }

    private static PosFavoritesController CreateController(AppDbContext ctx, string role = "Cashier")
    {
        var controller = new PosFavoritesController(ctx, TenantTestDoubles.TenantAccessorReturning(TenantId));
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, CashierId),
            new(ClaimTypes.Role, role),
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
            },
        };
        return controller;
    }

    [Fact]
    public async Task GetFavorites_ReturnsMappedDtos()
    {
        await using var ctx = CreateContext();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Name = "Latte",
            Price = 4.2m,
            TaxType = 1,
            CategoryId = Guid.NewGuid(),
        };
        ctx.Products.Add(product);
        ctx.CashierFavorites.Add(new CashierFavorite
        {
            TenantId = TenantId,
            CashierId = CashierId,
            ProductId = product.Id,
            SortOrder = 0,
        });
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx);
        var result = await controller.GetFavorites(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<List<CashierFavoriteDto>>(ok.Value);
        Assert.Single(list);
        Assert.Equal("Latte", list[0].ProductName);
        Assert.Equal(product.Id, list[0].ProductId);
    }

    [Fact]
    public async Task GetFavorites_ForSuperAdmin_ReturnsEmptyList()
    {
        await using var ctx = CreateContext();
        var controller = CreateController(ctx, "SuperAdmin");

        var result = await controller.GetFavorites(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<List<CashierFavoriteDto>>(ok.Value);
        Assert.Empty(list);
    }

    [Fact]
    public async Task AddFavorite_ThenRemoveById_DeletesRow()
    {
        await using var ctx = CreateContext();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Name = "Cappuccino",
            Price = 3.8m,
            TaxType = 1,
            CategoryId = Guid.NewGuid(),
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx);
        var addResult = await controller.AddFavorite(new AddFavoriteRequest { ProductId = product.Id }, CancellationToken.None);
        Assert.IsType<OkResult>(addResult);

        var fav = await ctx.CashierFavorites.SingleAsync(f => f.CashierId == CashierId);
        var removeResult = await controller.RemoveFavorite(fav.Id, CancellationToken.None);
        Assert.IsType<OkResult>(removeResult);
        Assert.Empty(await ctx.CashierFavorites.ToListAsync());
    }

    [Fact]
    public async Task ReorderFavorites_UpdatesSortOrder()
    {
        await using var ctx = CreateContext();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        ctx.Products.AddRange(
            new Product { Id = p1, TenantId = TenantId, Name = "A", Price = 1m, TaxType = 1, CategoryId = Guid.NewGuid() },
            new Product { Id = p2, TenantId = TenantId, Name = "B", Price = 2m, TaxType = 1, CategoryId = Guid.NewGuid() });
        var f1 = new CashierFavorite { TenantId = TenantId, CashierId = CashierId, ProductId = p1, SortOrder = 0 };
        var f2 = new CashierFavorite { TenantId = TenantId, CashierId = CashierId, ProductId = p2, SortOrder = 1 };
        ctx.CashierFavorites.AddRange(f1, f2);
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx);
        var reorder = await controller.ReorderFavorites(
            new ReorderFavoritesRequest { OrderIds = new List<Guid> { f2.Id, f1.Id } },
            CancellationToken.None);
        Assert.IsType<OkResult>(reorder);

        var rows = await ctx.CashierFavorites.OrderBy(f => f.SortOrder).ToListAsync();
        Assert.Equal(f2.Id, rows[0].Id);
        Assert.Equal(f1.Id, rows[1].Id);
    }
}
