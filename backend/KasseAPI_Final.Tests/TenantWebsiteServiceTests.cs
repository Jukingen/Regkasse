using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Website;
using KasseAPI_Final.Sites;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantWebsiteServiceTests
{
    [Fact]
    public async Task GetWebsiteHtml_includes_live_menu()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options);
        var factory = new Factory(options);
        var tenantId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Live Cafe",
            Slug = "live-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Name = "Drinks",
            Key = "drinks",
            IsActive = true,
            SortOrder = 1
        });
        db.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Latte",
            Price = 3.8m,
            Category = "Drinks",
            CategoryId = categoryId,
            TaxType = 1,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var catalog = new PublicTenantCatalogService(factory);
        var menu = await catalog.GetMenuAsync("live-cafe");
        Assert.NotNull(menu);
        Assert.Contains(menu!.Items, i => i.Name == "Latte");

        var sut = new TenantWebsiteService(catalog);
        var html = await sut.GetWebsiteHtmlAsync("live-cafe", "modern");

        Assert.NotNull(html);
        Assert.Contains("Latte", html!, StringComparison.Ordinal);
        Assert.Contains("Speisekarte", html, StringComparison.Ordinal);
        Assert.Contains("live-cafe", html, StringComparison.Ordinal);
        Assert.Contains("3.80", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetWebsiteHtml_unknown_slug_returns_null()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var sut = new TenantWebsiteService(new PublicTenantCatalogService(new Factory(options)));
        Assert.Null(await sut.GetWebsiteHtmlAsync("nope"));
    }

    private sealed class Factory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public Factory(DbContextOptions<AppDbContext> options) => _options = options;
        public AppDbContext CreateDbContext() => new(_options);
        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            new(new AppDbContext(_options));
    }
}
