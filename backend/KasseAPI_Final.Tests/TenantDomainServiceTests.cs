using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Services.Website;
using KasseAPI_Final.Sites;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
namespace KasseAPI_Final.Tests;

public sealed class TenantDomainServiceTests
{
    [Fact]
    public async Task Add_verify_and_resolve_by_host()
    {
        var (sut, _, tenantId) = await CreateSutAsync();

        var add = await sut.AddDomainAsync(tenantId, "Cafe-Muster.at");
        Assert.True(add.Succeeded);
        Assert.Equal("cafe-muster.at", add.Domain!.Domain);
        Assert.False(add.Domain.IsVerified);
        Assert.False(string.IsNullOrWhiteSpace(add.Domain.VerificationToken));

        var bad = await sut.VerifyAsync(tenantId, add.Domain.Id, "wrong");
        Assert.False(bad.Succeeded);

        var ok = await sut.VerifyAsync(tenantId, add.Domain.Id, add.Domain.VerificationToken);
        Assert.True(ok.Succeeded);
        Assert.True(ok.Domain!.IsVerified);

        var slug = await sut.TryResolveSlugByHostAsync("www.cafe-muster.at");
        Assert.Equal("demo-cafe", slug);

        await sut.SetWebsiteEnabledAsync(tenantId, add.Domain.Id, false);
        Assert.Null(await sut.TryResolveSlugByHostAsync("cafe-muster.at"));
    }

    [Fact]
    public async Task Publish_writes_index_html()
    {
        var root = Path.Combine(Path.GetTempPath(), "regkasse-sites-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var (sut, db, tenantId) = await CreateSutAsync(root);
            var categoryId = Guid.NewGuid();
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
                Name = "Tea",
                Price = 2m,
                Category = "Drinks",
                CategoryId = categoryId,
                TaxType = 1,
                IsActive = true
            });
            await db.SaveChangesAsync();

            var publish = await sut.PublishStaticSiteAsync(tenantId, "modern");
            Assert.True(publish.Succeeded);
            Assert.NotNull(publish.Url);

            var index = Path.Combine(root, "demo-cafe", "index.html");
            Assert.True(File.Exists(index));
            var html = await File.ReadAllTextAsync(index);
            Assert.Contains("Tea", html, StringComparison.Ordinal);
        }
        finally
        {
            try
            { Directory.Delete(root, true); }
            catch { /* ignore */ }
        }
    }

    private static async Task<(TenantDomainService Sut, AppDbContext Db, Guid TenantId)> CreateSutAsync(
        string? websiteRoot = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
        var factory = new Factory(options);
        var tenantId = Guid.NewGuid();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Demo Cafe",
            Slug = "demo-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(websiteRoot ?? Path.GetTempPath());

        var website = new TenantWebsiteService(new PublicTenantCatalogService(factory));
        var sut = new TenantDomainService(
            factory,
            website,
            Options.Create(new WebsiteGeneratorOptions
            {
                Enabled = true,
                RootRelativeDirectory = websiteRoot is null ? "App_Data/generated-websites" : "",
                PublicUrlPathPrefix = "/media/sites"
            }),
            env.Object,
            TimeProvider.System,
            NullLogger<TenantDomainService>.Instance);

        return (sut, db, tenantId);
    }

    private sealed class Factory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public Factory(DbContextOptions<AppDbContext> options) => _options = options;
        public AppDbContext CreateDbContext() => new(_options);
        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            new(new AppDbContext(_options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary)));
    }
}
