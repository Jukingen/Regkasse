using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DemoProductImportServiceTests
{
    private static string ResolveBackendContentRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Data", "demo-products.json");
            if (File.Exists(candidate))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate backend/Data/demo-products.json for tests.");
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"demo_import_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static DemoProductImportService CreateService(AppDbContext db)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(ResolveBackendContentRoot());
        return new DemoProductImportService(
            db,
            env.Object,
            Mock.Of<IDemoProductImportImageService>(),
            Mock.Of<ILogger<DemoProductImportService>>());
    }

    private static async Task<Tenant> SeedTenantAsync(AppDbContext db)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Pizzeria Demo",
            Slug = "pizzeria-demo",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    [Fact]
    public async Task ImportDemoProductsAsync_CreatesCategoriesAndProducts()
    {
        await using var db = CreateDb();
        var tenant = await SeedTenantAsync(db);
        var service = CreateService(db);

        var result = await service.ImportDemoProductsAsync(
            tenant.Id,
            new DemoImportRequest { SelectedCategories = ["Salate", "Pasta"] });

        Assert.True(result.Success);
        Assert.Equal(16, result.TotalProductCount);
        Assert.Equal(2, result.SelectedCategoryCount);
        Assert.Equal(16, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(16, result.ImportedProductCount);
        Assert.Equal(2, result.CategoriesCreated);
        Assert.True(result.AverageImportedPrice > 0);
        Assert.NotEmpty(result.CategoryIds);
        Assert.Equal(result.Created, result.ProductIds.Count);
        Assert.Equal(2, result.CategorySummaries.Count);
        Assert.Contains(result.CategorySummaries, s => s.CategoryName == "Salate" && s.ProductCount > 0 && s.Created == s.ProductCount);
        Assert.Contains(result.CategorySummaries, s => s.CategoryName == "Pasta" && s.ProductCount > 0 && s.Created == s.ProductCount);

        var categories = await db.Categories.IgnoreQueryFilters()
            .Where(c => c.TenantId == tenant.Id)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();
        Assert.Equal(["Salate", "Pasta"], categories.Select(c => c.Name));
        Assert.All(categories, c => Assert.False(string.IsNullOrWhiteSpace(c.Key)));
        Assert.Contains(categories, c => c.Key == "salate" && c.OriginalDemoName == "Salate" && c.IsSystemCategory);
        Assert.Contains(categories, c => c.Key == "pasta" && c.Name == "Pasta" && c.IsSystemCategory);

        var chefsalat = await db.Products.IgnoreQueryFilters()
            .SingleAsync(p => p.TenantId == tenant.Id && p.Name == "chefsalat");
        Assert.Equal(9.50m, chefsalat.Price);
        Assert.Equal("Salate", chefsalat.Category);
    }

    [Fact]
    public async Task ImportDemoProductsAsync_ImportsFullCatalogWhenUnfiltered()
    {
        await using var db = CreateDb();
        var tenant = await SeedTenantAsync(db);
        var service = CreateService(db);

        var result = await service.ImportDemoProductsAsync(tenant.Id, new DemoImportRequest());

        Assert.True(result.Success);
        Assert.Equal(156, result.TotalProductCount);
        Assert.Equal(16, result.SelectedCategoryCount);
        Assert.Equal(156, result.Created);
        Assert.Equal(156, result.ImportedProductCount);
        Assert.Equal(16, result.CategoriesCreated);

        var categories = await db.Categories.IgnoreQueryFilters()
            .Where(c => c.TenantId == tenant.Id)
            .ToListAsync();
        Assert.Equal(16, categories.Count);
        Assert.Contains(categories, c => c.Key == "saucen" && c.IsSystemCategory);
    }

    [Fact]
    public async Task ImportDemoProductsAsync_SkipsExistingProductsUnlessOverwrite()
    {
        await using var db = CreateDb();
        var tenant = await SeedTenantAsync(db);
        var service = CreateService(db);

        var first = await service.ImportDemoProductsAsync(tenant.Id, new DemoImportRequest());
        Assert.True(first.Success);

        var second = await service.ImportDemoProductsAsync(tenant.Id, new DemoImportRequest());
        Assert.True(second.Success);
        Assert.Equal(0, second.Created);
        Assert.Equal(0, second.Updated);
        Assert.True(second.Skipped >= 156);
        Assert.All(second.CategorySummaries, s => Assert.Equal(s.ProductCount, s.Skipped));

        var overwrite = await service.ImportDemoProductsAsync(
            tenant.Id,
            new DemoImportRequest { OverwriteExisting = true });
        Assert.True(overwrite.Success);
        Assert.Equal(0, overwrite.Created);
        Assert.True(overwrite.Updated >= 156);
        Assert.Equal(0, overwrite.CategoriesCreated);
        Assert.True(overwrite.ImportedProductCount >= 156);
    }

    [Fact]
    public async Task ImportDemoProductsAsync_ReusesExistingCategoryWhenNameMatchesButKeyDiffers()
    {
        await using var db = CreateDb();
        var tenant = await SeedTenantAsync(db);
        db.Categories.Add(new Category
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Key = "salate-2",
            Name = "Salate",
            VatRate = 10m,
            FiscalCategory = RksvProductCategory.Food,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ImportDemoProductsAsync(
            tenant.Id,
            new DemoImportRequest { SelectedCategories = ["Salate"] });

        Assert.True(result.Success);
        Assert.Equal(0, result.CategoriesCreated);
        Assert.Equal(1, await db.Categories.IgnoreQueryFilters().CountAsync(c => c.TenantId == tenant.Id && c.Name == "Salate"));
    }

    [Fact]
    public async Task ImportDemoProductsAsync_ReturnsErrorWhenTenantMissing()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var result = await service.ImportDemoProductsAsync(Guid.NewGuid(), new DemoImportRequest());

        Assert.False(result.Success);
        Assert.Equal("Tenant not found.", result.ErrorMessage);
    }

    [SkippableFact]
    public async Task ImportDemoProductsAsync_PostgreSql_DoubleImport_NoDuplicateCategoryErrors()
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        Skip.If(string.IsNullOrWhiteSpace(cs), "Set ConnectionStrings__DefaultConnection for local PG verification.");

        var options = new DbContextOptionsBuilder<AppDbContext>().UseAppNpgsql(cs).Options;
        await using var db = new AppDbContext(options);
        var tenantId = DemoTenantIds.Cafe;
        Skip.If(!await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == tenantId), "Cafe tenant missing; run migrations first.");

        // Clean prior partial runs for idempotent local verification.
        var existingProducts = await db.Products.IgnoreQueryFilters().Where(p => p.TenantId == tenantId).ToListAsync();
        var existingCategories = await db.Categories.IgnoreQueryFilters().Where(c => c.TenantId == tenantId).ToListAsync();
        if (existingProducts.Count > 0 || existingCategories.Count > 0)
        {
            db.Products.RemoveRange(existingProducts);
            db.Categories.RemoveRange(existingCategories);
            await db.SaveChangesAsync();
        }

        var service = CreateService(db);
        var first = await service.ImportDemoProductsAsync(tenantId, new DemoImportRequest());
        Assert.True(first.Success, first.ErrorMessage);
        Assert.Equal(16, first.CategoriesCreated);
        Assert.Equal(16, await db.Categories.IgnoreQueryFilters().CountAsync(c => c.TenantId == tenantId));

        var second = await service.ImportDemoProductsAsync(tenantId, new DemoImportRequest());
        Assert.True(second.Success, second.ErrorMessage);
        Assert.Equal(0, second.CategoriesCreated);
        Assert.Equal(16, await db.Categories.IgnoreQueryFilters().CountAsync(c => c.TenantId == tenantId));
    }
}
