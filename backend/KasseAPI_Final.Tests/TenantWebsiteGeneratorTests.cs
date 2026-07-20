using System.IO.Compression;
using System.Text;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Website;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantWebsiteGeneratorTests
{
    [Fact]
    public async Task GenerateWebsiteAsync_rejects_invalid_domain()
    {
        var (sut, root, _, _) = await CreateSutAsync();
        try
        {
            var result = await sut.GenerateWebsiteAsync(Guid.NewGuid(), "not a domain");
            Assert.False(result.Succeeded);
            Assert.Equal(TenantWebsiteGenerator.InvalidDomainCode, result.Code);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task GenerateWebsiteAsync_builds_zip_with_menu_and_deploy_script()
    {
        var (sut, root, db, tenantId) = await CreateSutAsync();
        try
        {
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
                Name = "Espresso",
                Price = 2.5m,
                Category = "Drinks",
                CategoryId = categoryId,
                TaxType = 1,
                IsActive = true
            });
            await db.SaveChangesAsync();

            var result = await sut.GenerateWebsiteAsync(tenantId, "https://www.Cafe-Muster.at", "modern");

            Assert.True(result.Succeeded);
            Assert.Equal("cafe-muster.at", result.Domain);
            Assert.Equal("/media/sites/demo-cafe/", result.PublishedUrl);
            Assert.NotNull(result.ZipFile);
            Assert.False(string.IsNullOrWhiteSpace(result.Script));
            Assert.Contains("cafe-muster.at", result.Instructions!, StringComparison.Ordinal);
            Assert.DoesNotContain("/var/www/html", result.Instructions!, StringComparison.Ordinal);

            await using var zip = new ZipArchive(new MemoryStream(result.ZipFile!), ZipArchiveMode.Read);
            Assert.Contains(zip.Entries, e => e.Name == "index.html");
            Assert.Contains(zip.Entries, e => e.Name == "styles.css");
            Assert.Contains(zip.Entries, e => e.Name == "app.js");
            Assert.Contains(zip.Entries, e => e.Name == "deploy.sh");
            Assert.Contains(zip.Entries, e => e.Name == "INSTRUCTIONS.txt");

            Assert.Contains(zip.Entries, e => e.FullName.Replace('\\', '/') == "assets/default-logo.svg"
                || e.Name == "default-logo.svg");

            var indexEntry = zip.GetEntry("index.html")!;
            using var reader = new StreamReader(indexEntry.Open(), Encoding.UTF8);
            var html = await reader.ReadToEndAsync();
            Assert.Contains("Espresso", html, StringComparison.Ordinal);
            Assert.Contains("Speisekarte", html, StringComparison.Ordinal);
            Assert.Contains("Demo Cafe", html, StringComparison.Ordinal);

            Assert.True(File.Exists(Path.Combine(root, "demo-cafe", "index.html")));
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static async Task<(TenantWebsiteGenerator Sut, string Root, AppDbContext Db, Guid TenantId)> CreateSutAsync()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "regkasse-tenant-website-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options);
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

        var contentRoot = Path.GetDirectoryName(root)!;
        var relative = Path.GetFileName(root)!;
        var websiteOpts = Options.Create(new WebsiteGeneratorOptions
        {
            Enabled = true,
            RootRelativeDirectory = relative,
            PublicUrlPathPrefix = "/media/sites"
        });
        var env = new TestEnv { ContentRootPath = contentRoot };

        var website = new WebsiteGeneratorService(
            factory,
            websiteOpts,
            env,
            NullLogger<WebsiteGeneratorService>.Instance);

        var sut = new TenantWebsiteGenerator(
            website,
            websiteOpts,
            env,
            NullLogger<TenantWebsiteGenerator>.Instance);

        return (sut, root, db, tenantId);
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
            // ignore
        }
    }

    private sealed class Factory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public Factory(DbContextOptions<AppDbContext> options) => _options = options;
        public AppDbContext CreateDbContext() => new(_options);
        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            new(new AppDbContext(_options));
    }

    private sealed class TestEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
