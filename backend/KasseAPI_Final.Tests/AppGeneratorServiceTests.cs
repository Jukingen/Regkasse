using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.App;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AppGeneratorServiceTests
{
    [Fact]
    public async Task GenerateAppAsync_Pwa_writes_installable_files()
    {
        var tenantId = Guid.NewGuid();
        var (sut, root, db) = CreateSut(nameof(GenerateAppAsync_Pwa_writes_installable_files));
        try
        {
            await SeedAsync(db, tenantId, "cafe-app");
            var result = await sut.GenerateAppAsync(tenantId, AppType.Pwa);

            Assert.True(result.Succeeded);
            Assert.Equal(AppType.Pwa, result.AppType);
            Assert.Equal("/media/sites/cafe-app/app/", result.DownloadUrl);
            Assert.True(File.Exists(Path.Combine(root, "cafe-app", "app", "index.html")));
            Assert.True(File.Exists(Path.Combine(root, "cafe-app", "app", "config.json")));
            Assert.True(File.Exists(Path.Combine(root, "cafe-app", "app", "manifest.webmanifest")));

            var html = await File.ReadAllTextAsync(Path.Combine(root, "cafe-app", "app", "index.html"));
            Assert.Contains("Espresso", html);
            Assert.Contains("Getr", html); // Getränke may be HTML-encoded (ä)
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task GenerateAppAsync_Native_writes_zip_package()
    {
        var tenantId = Guid.NewGuid();
        var (sut, root, db) = CreateSut(nameof(GenerateAppAsync_Native_writes_zip_package));
        try
        {
            await SeedAsync(db, tenantId, "native-cafe");
            var result = await sut.GenerateAppAsync(tenantId, AppType.Native);

            Assert.True(result.Succeeded);
            Assert.Equal(AppType.Native, result.AppType);
            Assert.Equal("/media/sites/native-cafe/app-native/app-source.zip", result.DownloadUrl);
            Assert.True(File.Exists(Path.Combine(root, "native-cafe", "app-native", "app-source.zip")));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task GenerateAppAsync_fails_when_tenant_missing()
    {
        var (sut, root, _) = CreateSut(nameof(GenerateAppAsync_fails_when_tenant_missing));
        try
        {
            var result = await sut.GenerateAppAsync(Guid.NewGuid(), AppType.Pwa);
            Assert.False(result.Succeeded);
            Assert.Equal(AppGeneratorService.TenantNotFoundCode, result.Code);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static async Task SeedAsync(AppDbContext db, Guid tenantId, string slug)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe Tenant",
            Slug = slug,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        db.CompanySettings.Add(new CompanySettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyName = "Demo Cafe",
            CompanyAddress = "Wien 1",
            CompanyTaxNumber = "ATU12345678",
            CompanyLogo = "/media/logo.png",
            Currency = "EUR",
            Language = "de",
            TimeZone = "Europe/Vienna",
            DateFormat = "dd.MM.yyyy",
            TimeFormat = "HH:mm",
            TaxCalculationMethod = "inclusive",
            InvoiceNumbering = "INV",
            ReceiptNumbering = "R",
            DefaultPaymentMethod = "cash",
            BusinessHours = new Dictionary<string, string>(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        var categoryId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Key = "drinks",
            Name = "Getränke",
            Color = "#8b4513",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        db.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Espresso",
            Price = 2.50m,
            TaxType = 1,
            Category = "drinks",
            CategoryId = categoryId,
            StockQuantity = 10,
            MinStockLevel = 1,
            Unit = "Stk",
            Barcode = "ESP001",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private static (AppGeneratorService Sut, string Root, AppDbContext Db) CreateSut(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "regkasse-app-tests", name + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var relative = Path.GetFileName(root)!;
        var contentRoot = Path.GetDirectoryName(root)!;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options);

        var sut = new AppGeneratorService(
            new Factory(options),
            Options.Create(new WebsiteGeneratorOptions
            {
                Enabled = true,
                RootRelativeDirectory = relative,
                PublicUrlPathPrefix = "/media/sites"
            }),
            new Env { ContentRootPath = contentRoot },
            NullLogger<AppGeneratorService>.Instance);

        return (sut, root, db);
    }

    private static void Cleanup(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
        catch
        {
            // best-effort
        }
    }

    private sealed class Factory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public Factory(DbContextOptions<AppDbContext> options) => _options = options;
        public AppDbContext CreateDbContext() => new(_options);
        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AppDbContext(_options));
    }

    private sealed class Env : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
