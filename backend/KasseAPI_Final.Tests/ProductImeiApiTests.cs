using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Imei;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ProductImeiApiTests
{
    [Fact]
    public async Task Add_ThenListInStock_ReturnsImei()
    {
        var tenantId = SystemTenantIds.Platform;
        await using var db = CreateDb(tenantId);
        TenantTestDoubles.EnsurePlatformTenant(db);
        var productId = await SeedTrackedProductAsync(db, tenantId);

        var controller = CreateController(db);
        var created = await controller.Add(
            productId,
            new AddProductImeiRequest { Imei = "490154203237518", WarrantyMonths = 24 },
            CancellationToken.None);

        var createdResult = Assert.IsType<ObjectResult>(created.Result);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
        var dto = Assert.IsType<ProductImeiDto>(createdResult.Value);
        Assert.Equal("490154203237518", dto.Imei);
        Assert.Equal(ProductImeiStatus.InStock, dto.Status);
        Assert.Equal(24, dto.WarrantyMonths);

        var list = await controller.List(productId, ProductImeiStatus.InStock, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(list.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<ProductImeiDto>>(ok.Value);
        Assert.Single(items);
        Assert.Equal(dto.Id, items[0].Id);
    }

    [Fact]
    public async Task Add_DuplicateImei_Returns409()
    {
        var tenantId = SystemTenantIds.Platform;
        await using var db = CreateDb(tenantId);
        TenantTestDoubles.EnsurePlatformTenant(db);
        var productId = await SeedTrackedProductAsync(db, tenantId);
        var controller = CreateController(db);
        var request = new AddProductImeiRequest { Imei = "356938035643809" };

        var first = await controller.Add(productId, request, CancellationToken.None);
        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ObjectResult>(first.Result).StatusCode);

        var second = await controller.Add(productId, request, CancellationToken.None);
        var conflict = Assert.IsType<ObjectResult>(second.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        var error = Assert.IsType<ProductImeiErrorDto>(conflict.Value);
        Assert.Equal(ProductImeiErrorCodes.Duplicate, error.Code);
    }

    private static PosProductImeisController CreateController(AppDbContext db)
    {
        var accessor = TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform);
        var controller = new PosProductImeisController(new ProductImeiService(db, accessor), accessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.Role, Roles.Cashier)],
                        authenticationType: "test")),
                },
            },
        };
        return controller;
    }

    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ImeiApi_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static async Task<Guid> SeedTrackedProductAsync(AppDbContext db, Guid tenantId)
    {
        var categoryId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Name = "Handys",
            VatRate = 20m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var productId = Guid.NewGuid();
        db.Products.Add(new Product
        {
            Id = productId,
            TenantId = tenantId,
            Name = "Pixel",
            Price = 199m,
            CategoryId = categoryId,
            Category = "Handys",
            StockQuantity = 5,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Standard,
            TaxRate = 20m,
            Barcode = $"imei-{productId:N}",
            ImeiTracked = true,
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true,
            Description = string.Empty,
        });
        await db.SaveChangesAsync();
        return productId;
    }
}
