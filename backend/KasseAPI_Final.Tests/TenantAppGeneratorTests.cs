using System.IO.Compression;
using System.Text;
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

public sealed class TenantAppGeneratorTests
{
    [Fact]
    public async Task GenerateAppAsync_native_returns_zip_with_instructions()
    {
        var tenantId = Guid.NewGuid();
        var (sut, root, db) = CreateSut(nameof(GenerateAppAsync_native_returns_zip_with_instructions));
        try
        {
            await SeedAsync(db, tenantId, "pack-cafe");
            var result = await sut.GenerateAppAsync(tenantId, AppType.Native);

            Assert.True(result.Succeeded);
            Assert.Equal(AppType.Native, result.AppType);
            Assert.NotNull(result.ZipFile);
            Assert.Contains("iOS:", result.Instructions!, StringComparison.Ordinal);
            Assert.Contains("Android:", result.Instructions!, StringComparison.Ordinal);
            Assert.Equal("/media/sites/pack-cafe/app-native/app-source.zip", result.DownloadUrl);

            await using var zip = new ZipArchive(new MemoryStream(result.ZipFile!), ZipArchiveMode.Read);
            Assert.Contains(zip.Entries, e => e.Name == "app-source.zip");
            Assert.Contains(zip.Entries, e => e.Name == "INSTRUCTIONS.txt");

            var instructionsEntry = zip.GetEntry("INSTRUCTIONS.txt")!;
            using var reader = new StreamReader(instructionsEntry.Open(), Encoding.UTF8);
            var text = await reader.ReadToEndAsync();
            Assert.Contains("Expo", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task GenerateAppAsync_pwa_returns_zip_with_index()
    {
        var tenantId = Guid.NewGuid();
        var (sut, root, db) = CreateSut(nameof(GenerateAppAsync_pwa_returns_zip_with_index));
        try
        {
            await SeedAsync(db, tenantId, "pwa-pack");
            var result = await sut.GenerateAppAsync(tenantId, AppType.Pwa);

            Assert.True(result.Succeeded);
            Assert.Equal(AppType.Pwa, result.AppType);
            Assert.NotNull(result.ZipFile);

            await using var zip = new ZipArchive(new MemoryStream(result.ZipFile!), ZipArchiveMode.Read);
            Assert.Contains(zip.Entries, e =>
                e.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase)
                || e.FullName.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(zip.Entries, e => e.Name == "INSTRUCTIONS.txt");
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
            var result = await sut.GenerateAppAsync(Guid.NewGuid(), AppType.Native);
            Assert.False(result.Succeeded);
            Assert.Equal(TenantAppGenerator.TenantNotFoundCode, result.Code);
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

    private static (TenantAppGenerator Sut, string Root, AppDbContext Db) CreateSut(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "regkasse-tenant-app", name + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var relative = Path.GetFileName(root)!;
        var contentRoot = Path.GetDirectoryName(root)!;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options);
        var websiteOpts = Options.Create(new WebsiteGeneratorOptions
        {
            Enabled = true,
            RootRelativeDirectory = relative,
            PublicUrlPathPrefix = "/media/sites"
        });
        var env = new Env { ContentRootPath = contentRoot };

        var app = new AppGeneratorService(
            new Factory(options),
            websiteOpts,
            env,
            NullLogger<AppGeneratorService>.Instance);

        var sut = new TenantAppGenerator(
            app,
            websiteOpts,
            env,
            NullLogger<TenantAppGenerator>.Instance);

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
