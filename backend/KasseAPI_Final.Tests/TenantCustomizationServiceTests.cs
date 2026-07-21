using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Website;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace KasseAPI_Final.Tests;

public sealed class TenantCustomizationServiceTests
{
    [Fact]
    public async Task Upsert_and_get_applies_theme_fields()
    {
        var (sut, _, tenantId) = await CreateSutAsync();

        var result = await sut.UpsertAsync(
            tenantId,
            new TenantCustomizationUpsert
            {
                Surface = "website",
                PrimaryColor = "#112233",
                SecondaryColor = "#aabbcc",
                BackgroundColor = "#ffffff",
                TextColor = "#000000",
                FontFamily = "Georgia, serif",
                Pages = ["home", "menu", "evil"],
                Features = ["live-menu", "loyalty", "hack"],
                CustomCss = "body{outline:1px solid red;}</style><script>",
                CustomJs = "console.log(1);</script>alert(1)"
            });

        Assert.True(result.Succeeded);
        Assert.Equal("#112233", result.Customization!.PrimaryColor);
        Assert.DoesNotContain("</style>", result.Customization.CustomCss!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("</script>", result.Customization.CustomJs!, StringComparison.OrdinalIgnoreCase);

        var loaded = await sut.GetOrDefaultAsync(tenantId, "website");
        Assert.Equal("#112233", loaded.PrimaryColor);
        var pages = TenantCustomizationService.ParseJsonList(loaded.PagesJson, TenantCustomization.DefaultPages);
        Assert.Contains("home", pages);
        Assert.Contains("menu", pages);
        Assert.DoesNotContain("evil", pages);
        var features = TenantCustomizationService.ParseJsonList(loaded.FeaturesJson, ["live-menu"]);
        Assert.Contains("loyalty", features);
        Assert.DoesNotContain("hack", features);
    }

    [Fact]
    public async Task Upsert_rejects_invalid_hex()
    {
        var (sut, _, tenantId) = await CreateSutAsync();
        var result = await sut.UpsertAsync(
            tenantId,
            new TenantCustomizationUpsert
            {
                Surface = "app",
                PrimaryColor = "not-a-color"
            });
        Assert.False(result.Succeeded);
        Assert.Equal(TenantCustomizationService.ValidationFailedCode, result.Code);
    }

    private static async Task<(TenantCustomizationService Sut, AppDbContext Db, Guid TenantId)> CreateSutAsync()
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
            Name = "Cafe",
            Slug = "cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new TenantCustomizationService(
            factory,
            TimeProvider.System,
            NullLogger<TenantCustomizationService>.Instance);
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
