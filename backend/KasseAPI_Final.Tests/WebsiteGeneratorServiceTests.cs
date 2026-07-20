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

public sealed class WebsiteGeneratorServiceTests
{
    [Fact]
    public async Task GenerateWebsiteAsync_fails_when_tenant_missing()
    {
        var (sut, root) = CreateSut(nameof(GenerateWebsiteAsync_fails_when_tenant_missing));
        try
        {
            var result = await sut.GenerateWebsiteAsync(Guid.NewGuid(), "modern");
            Assert.False(result.Succeeded);
            Assert.Equal(WebsiteGeneratorService.TenantNotFoundCode, result.Code);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task GenerateWebsiteAsync_fails_when_template_unknown()
    {
        var tenantId = Guid.NewGuid();
        var (sut, root, db) = CreateSutWithDb(nameof(GenerateWebsiteAsync_fails_when_template_unknown));
        try
        {
            await SeedTenantAsync(db, tenantId, "acme");
            var result = await sut.GenerateWebsiteAsync(tenantId, "unknown");
            Assert.False(result.Succeeded);
            Assert.Equal(WebsiteGeneratorService.TemplateNotFoundCode, result.Code);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task GenerateWebsiteAsync_writes_files_and_returns_url()
    {
        var tenantId = Guid.NewGuid();
        var (sut, root, db) = CreateSutWithDb(nameof(GenerateWebsiteAsync_writes_files_and_returns_url));
        try
        {
            await SeedTenantAsync(db, tenantId, "acme-cafe", companyName: "Acme Café", description: "Gutes Essen");
            var result = await sut.GenerateWebsiteAsync(tenantId, "modern");

            Assert.True(result.Succeeded);
            Assert.Equal("Modern", result.TemplateName);
            Assert.Equal("modern", result.TemplateId);
            Assert.Equal("/media/sites/acme-cafe/", result.Url);
            Assert.NotNull(result.Progress);
            Assert.Equal(100, result.Progress!.Percent);
            Assert.Equal("Complete", result.Progress.Stage);

            var index = Path.Combine(root, "acme-cafe", "index.html");
            var css = Path.Combine(root, "acme-cafe", "styles.css");
            var js = Path.Combine(root, "acme-cafe", "app.js");
            var logo = Path.Combine(root, "acme-cafe", "assets", "default-logo.svg");
            Assert.True(File.Exists(index));
            Assert.True(File.Exists(css));
            Assert.True(File.Exists(js));
            Assert.True(File.Exists(logo));

            var html = await File.ReadAllTextAsync(index);
            Assert.Contains("Acme Caf", html);
            Assert.Contains("Gutes Essen", html);
            Assert.Contains(WebsiteGeneratorService.DefaultLogoRelativePath, html);
            Assert.DoesNotContain("<script>alert", html);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task GenerateWebsiteAsync_embeds_menu_products_and_categories()
    {
        var tenantId = Guid.NewGuid();
        var (sut, root, db) = CreateSutWithDb(nameof(GenerateWebsiteAsync_embeds_menu_products_and_categories));
        try
        {
            await SeedTenantAsync(db, tenantId, "menu-cafe", companyName: "Menu Cafe");
            var categoryId = Guid.NewGuid();
            db.Categories.Add(new Category
            {
                Id = categoryId,
                TenantId = tenantId,
                Name = "Getränke",
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
                Category = "Getränke",
                CategoryId = categoryId,
                TaxType = 1,
                IsActive = true
            });
            await db.SaveChangesAsync();

            var result = await sut.GenerateWebsiteAsync(tenantId, "classic");
            Assert.True(result.Succeeded);
            Assert.Equal(1, result.MenuItemCount);
            Assert.Equal(1, result.CategoryCount);

            var html = await File.ReadAllTextAsync(Path.Combine(root, "menu-cafe", "index.html"));
            Assert.Contains("Espresso", html);
            Assert.Contains("Speisekarte", html);
            Assert.Contains("Getr", html);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task PreviewWebsiteAsync_applies_unsaved_color_overrides()
    {
        var tenantId = Guid.NewGuid();
        var (sut, root, db) = CreateSutWithDb(nameof(PreviewWebsiteAsync_applies_unsaved_color_overrides));
        try
        {
            await SeedTenantAsync(db, tenantId, "override-cafe", companyName: "Override Cafe");
            var result = await sut.PreviewWebsiteAsync(
                tenantId,
                "modern",
                new WebsitePreviewOverrides
                {
                    PrimaryColor = "#ff0000",
                    BackgroundColor = "#00ff00",
                    CustomCss = ".hero { outline: 2px solid red; }"
                });

            Assert.True(result.Succeeded);
            Assert.Contains("--bg: #00ff00", result.Css);
            Assert.Contains(".hero { outline: 2px solid red; }", result.Css);
            Assert.False(Directory.Exists(Path.Combine(root, "override-cafe")));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task PreviewWebsiteAsync_returns_html_without_writing_files()
    {
        var tenantId = Guid.NewGuid();
        var (sut, root, db) = CreateSutWithDb(nameof(PreviewWebsiteAsync_returns_html_without_writing_files));
        try
        {
            await SeedTenantAsync(db, tenantId, "preview-cafe", companyName: "Preview Cafe");
            var result = await sut.PreviewWebsiteAsync(tenantId, "minimal");

            Assert.True(result.Succeeded);
            Assert.Equal("minimal", result.TemplateId);
            Assert.Contains("Preview Cafe", result.Html);
            Assert.False(Directory.Exists(Path.Combine(root, "preview-cafe")));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task GenerateWebsiteAsync_encodes_html_in_company_name()
    {
        var tenantId = Guid.NewGuid();
        var (sut, root, db) = CreateSutWithDb(nameof(GenerateWebsiteAsync_encodes_html_in_company_name));
        try
        {
            await SeedTenantAsync(db, tenantId, "safe-slug", companyName: "<script>alert(1)</script>");
            var result = await sut.GenerateWebsiteAsync(tenantId, "minimal");
            Assert.True(result.Succeeded);
            var html = await File.ReadAllTextAsync(Path.Combine(root, "safe-slug", "index.html"));
            Assert.DoesNotContain("<script>alert(1)</script>", html);
            Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void GetTemplates_returns_three_built_ins()
    {
        var (sut, root) = CreateSut(nameof(GetTemplates_returns_three_built_ins));
        try
        {
            var templates = sut.GetTemplates();
            Assert.Equal(3, templates.Count);
            Assert.Contains(templates, t => t.Id == "modern");
            Assert.Contains(templates, t => t.Id == "classic");
            Assert.Contains(templates, t => t.Id == "minimal");
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static async Task SeedTenantAsync(
        AppDbContext db,
        Guid tenantId,
        string slug,
        string? companyName = null,
        string? description = null)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = companyName ?? "Tenant",
            Slug = slug,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        if (companyName is not null || description is not null)
        {
            db.CompanySettings.Add(new CompanySettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CompanyName = companyName ?? "Company",
                CompanyAddress = "Teststraße 1",
                CompanyTaxNumber = "ATU12345678",
                CompanyDescription = description,
                Currency = "EUR",
                Language = "de",
                TimeZone = "Europe/Vienna",
                DateFormat = "dd.MM.yyyy",
                TimeFormat = "HH:mm",
                TaxCalculationMethod = "inclusive",
                InvoiceNumbering = "INV-{yyyy}-{seq}",
                ReceiptNumbering = "R-{seq}",
                DefaultPaymentMethod = "cash",
                BusinessHours = new Dictionary<string, string> { ["Mo-Fr"] = "09:00-18:00" },
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    private static (WebsiteGeneratorService Sut, string Root) CreateSut(string name)
    {
        var (sut, root, _) = CreateSutWithDb(name);
        return (sut, root);
    }

    private static (WebsiteGeneratorService Sut, string Root, AppDbContext Db) CreateSutWithDb(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "regkasse-website-tests", name + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options);
        var factory = new TestDbContextFactory(options);

        var relative = Path.GetFileName(root)!;
        var contentRoot = Path.GetDirectoryName(root)!;

        var sut = new WebsiteGeneratorService(
            factory,
            Options.Create(new WebsiteGeneratorOptions
            {
                Enabled = true,
                RootRelativeDirectory = relative,
                PublicUrlPathPrefix = "/media/sites"
            }),
            new TestHostEnvironment { ContentRootPath = contentRoot },
            NullLogger<WebsiteGeneratorService>.Instance);

        return (sut, root, db);
    }

    private static void Cleanup(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // best-effort temp cleanup
        }
    }

    private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory(DbContextOptions<AppDbContext> options) => _options = options;

        public AppDbContext CreateDbContext() => new(_options);

        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AppDbContext(_options));
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
