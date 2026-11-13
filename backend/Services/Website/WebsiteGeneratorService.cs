using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Website;

/// <summary>
/// One-click static website generator from FA tenant / company settings.
/// Publishes HTML/CSS/JS under local static files (CDN-ready URL prefix).
/// </summary>
public sealed class WebsiteGeneratorService : IWebsiteGeneratorService
{
    public const string DisabledCode = "WEBSITE_GENERATOR_DISABLED";
    public const string TenantNotFoundCode = "TENANT_NOT_FOUND";
    public const string TemplateNotFoundCode = "TEMPLATE_NOT_FOUND";
    public const string InvalidSlugCode = "INVALID_TENANT_SLUG";
    public const string DeployFailedCode = "WEBSITE_DEPLOY_FAILED";

    /// <summary>Fallback logo path relative to the published site directory.</summary>
    public const string DefaultLogoRelativePath = WebsiteTenantData.DefaultLogoRelativePath;

    private static readonly Regex SafeSlugRegex = new(
        @"^[a-z0-9]([a-z0-9-]{0,62}[a-z0-9])?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Minimal SVG placeholder when tenant has no logo upload.</summary>
    internal static readonly string DefaultLogoSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="128" height="128" viewBox="0 0 128 128" role="img" aria-label="Logo">
          <rect width="128" height="128" rx="24" fill="#0f172a"/>
          <text x="64" y="76" text-anchor="middle" font-family="system-ui,sans-serif" font-size="48" font-weight="700" fill="#38bdf8">R</text>
        </svg>
        """;

    private readonly List<WebsiteTemplate> _templates =
    [
        new WebsiteTemplate
        {
            Id = "modern",
            Name = "Modern",
            Description = "Clean, modern design",
            PreviewImage = "/templates/modern-preview.png"
        },
        new WebsiteTemplate
        {
            Id = "classic",
            Name = "Classic",
            Description = "Traditional restaurant style",
            PreviewImage = "/templates/classic-preview.png"
        },
        new WebsiteTemplate
        {
            Id = "minimal",
            Name = "Minimal",
            Description = "Simple and elegant",
            PreviewImage = "/templates/minimal-preview.png"
        }
    ];

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IOptions<WebsiteGeneratorOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly ITenantCustomizationService? _customizations;
    private readonly ILogger<WebsiteGeneratorService> _logger;

    public WebsiteGeneratorService(
        IDbContextFactory<AppDbContext> dbFactory,
        IOptions<WebsiteGeneratorOptions> options,
        IHostEnvironment environment,
        ILogger<WebsiteGeneratorService> logger,
        ITenantCustomizationService? customizations = null)
    {
        _dbFactory = dbFactory;
        _options = options;
        _environment = environment;
        _logger = logger;
        _customizations = customizations;
    }

    public IReadOnlyList<WebsiteTemplate> GetTemplates() => _templates;

    public async Task<WebsiteResult> GenerateWebsiteAsync(
        Guid tenantId,
        string templateId,
        CancellationToken ct = default)
    {
        var progress = new WebsiteGenerateProgress();
        progress.Update(5, "Validating request");

        var resolved = await ResolveGenerationAsync(tenantId, templateId, ct);
        if (!resolved.Succeeded)
            return WebsiteResult.Fail(resolved.Code!, resolved.Error!);

        var tenant = resolved.Tenant!;
        var template = resolved.Template!;
        progress.Update(20, "Loading tenant data");

        try
        {
            progress.Update(40, "Generating HTML");
            var html = await GenerateHtmlAsync(tenant, template, ct);

            progress.Update(60, "Generating CSS");
            var css = await GenerateCssAsync(tenant, template, ct);

            progress.Update(75, "Generating JavaScript");
            var js = await GenerateJsAsync(tenant, template, ct);

            progress.Update(90, "Publishing files");
            var url = await DeployToCdnAsync(tenant.Slug, html, css, js, ct);

            progress.Update(100, "Complete");
            return WebsiteResult.Success(
                url,
                template.Name,
                template.Id,
                progress,
                tenant.MenuItems.Count,
                tenant.Categories.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Website generation failed for tenant {TenantId} template {TemplateId}",
                tenantId,
                template.Id);
            return WebsiteResult.Fail(DeployFailedCode, "Website generation failed.");
        }
    }

    public async Task<WebsitePreviewResult> PreviewWebsiteAsync(
        Guid tenantId,
        string templateId,
        WebsitePreviewOverrides? overrides = null,
        CancellationToken ct = default)
    {
        var resolved = await ResolveGenerationAsync(tenantId, templateId, ct);
        if (!resolved.Succeeded)
            return WebsitePreviewResult.Fail(resolved.Code!, resolved.Error!);

        var tenant = ApplyPreviewOverrides(resolved.Tenant!, overrides);
        var template = resolved.Template!;

        try
        {
            var html = await GenerateHtmlAsync(tenant, template, ct);
            var css = await GenerateCssAsync(tenant, template, ct);
            var js = await GenerateJsAsync(tenant, template, ct);
            return WebsitePreviewResult.Success(
                html,
                css,
                js,
                template.Id,
                template.Name,
                tenant.LogoUrl,
                tenant.MenuItems.Count,
                tenant.Categories.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Website preview failed for tenant {TenantId} template {TemplateId}",
                tenantId,
                template.Id);
            return WebsitePreviewResult.Fail(DeployFailedCode, "Website preview failed.");
        }
    }

    /// <summary>Merge unsaved FA customization overrides onto a tenant snapshot for preview only.</summary>
    internal static WebsiteTenantData ApplyPreviewOverrides(
        WebsiteTenantData tenant,
        WebsitePreviewOverrides? overrides)
    {
        if (overrides is null)
            return tenant;

        var pages = overrides.Pages is not null ? overrides.Pages : tenant.Pages;
        var features = overrides.Features is not null ? overrides.Features : tenant.Features;

        return new WebsiteTenantData
        {
            TenantId = tenant.TenantId,
            Name = tenant.Name,
            Slug = tenant.Slug,
            CompanyName = tenant.CompanyName,
            Address = tenant.Address,
            Phone = tenant.Phone,
            Email = tenant.Email,
            Website = tenant.Website,
            Description = tenant.Description,
            LogoUrl = FirstNonEmpty(overrides.LogoUrl, tenant.LogoUrl) ?? DefaultLogoRelativePath,
            FaviconUrl = FirstNonEmpty(overrides.FaviconUrl, tenant.FaviconUrl),
            PrimaryColor = FirstNonEmpty(overrides.PrimaryColor, tenant.PrimaryColor),
            SecondaryColor = FirstNonEmpty(overrides.SecondaryColor, tenant.SecondaryColor),
            BackgroundColor = FirstNonEmpty(overrides.BackgroundColor, tenant.BackgroundColor),
            TextColor = FirstNonEmpty(overrides.TextColor, tenant.TextColor),
            FontFamily = FirstNonEmpty(overrides.FontFamily, tenant.FontFamily),
            CustomCss = overrides.CustomCss ?? tenant.CustomCss,
            CustomJs = overrides.CustomJs ?? tenant.CustomJs,
            Pages = pages,
            Features = features,
            Categories = tenant.Categories,
            MenuItems = tenant.MenuItems,
            BusinessHours = tenant.BusinessHours
        };
    }

    private static string? FirstNonEmpty(string? preferred, string? fallback) =>
        !string.IsNullOrWhiteSpace(preferred) ? preferred.Trim() : fallback;

    private async Task<GenerationResolveResult> ResolveGenerationAsync(
        Guid tenantId,
        string templateId,
        CancellationToken ct)
    {
        var opts = _options.Value;
        if (!opts.Enabled)
            return GenerationResolveResult.Fail(DisabledCode, "Website generator is disabled.");

        if (string.IsNullOrWhiteSpace(templateId))
            return GenerationResolveResult.Fail(TemplateNotFoundCode, "Template not found");

        var template = _templates.FirstOrDefault(t =>
            string.Equals(t.Id, templateId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (template is null)
            return GenerationResolveResult.Fail(TemplateNotFoundCode, "Template not found");

        var tenant = await GetTenantDataAsync(tenantId, ct);
        if (tenant is null)
            return GenerationResolveResult.Fail(TenantNotFoundCode, "Tenant not found.");

        if (!IsSafeSlug(tenant.Slug))
            return GenerationResolveResult.Fail(InvalidSlugCode, "Tenant slug is not valid for public hosting.");

        return GenerationResolveResult.Ok(tenant, template);
    }

    internal async Task<WebsiteTenantData?> GetTenantDataAsync(Guid tenantId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.Id == tenantId && t.IsActive && t.DeletedAtUtc == null,
                ct);
        if (tenant is null)
            return null;

        var company = await db.CompanySettings.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.IsActive, ct);

        TenantCustomization? custom = null;
        if (_customizations is not null)
            custom = await _customizations.GetAsync(tenantId, TenantCustomization.TypeWebsite, ct);

        var pages = TenantCustomizationService.ParseJsonList(
            custom?.PagesJson,
            TenantCustomization.DefaultPages);
        var features = TenantCustomizationService.ParseJsonList(
            custom?.FeaturesJson,
            ["live-menu"]);

        var categories = Array.Empty<WebsiteMenuCategory>();
        var menuItems = Array.Empty<WebsiteMenuItem>();
        if (features.Any(f => string.Equals(f, "live-menu", StringComparison.OrdinalIgnoreCase)))
        {
            categories = await db.Categories.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new WebsiteMenuCategory
                {
                    Id = c.Id,
                    Name = c.Name,
                    SortOrder = c.SortOrder
                })
                .ToArrayAsync(ct);

            menuItems = await db.Products.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(p => p.TenantId == tenantId && p.IsActive)
                .OrderBy(p => p.Name)
                .Take(200)
                .Select(p => new WebsiteMenuItem
                {
                    Id = p.Id,
                    Name = p.Name,
                    CategoryId = p.CategoryId,
                    CategoryName = p.CategoryNavigation != null ? p.CategoryNavigation.Name : p.Category,
                    Price = p.Price,
                    Description = p.Description
                })
                .ToArrayAsync(ct);
        }

        var logoUrl = !string.IsNullOrWhiteSpace(custom?.LogoUrl)
            ? custom.LogoUrl
            : company?.CompanyLogo;
        if (string.IsNullOrWhiteSpace(logoUrl))
            logoUrl = DefaultLogoRelativePath;

        return new WebsiteTenantData
        {
            TenantId = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            CompanyName = company?.CompanyName ?? tenant.Name,
            Address = company?.CompanyAddress ?? tenant.Address,
            Phone = company?.CompanyPhone ?? tenant.Phone,
            Email = company?.CompanyEmail ?? tenant.Email,
            Website = company?.CompanyWebsite,
            Description = company?.CompanyDescription,
            LogoUrl = logoUrl,
            FaviconUrl = custom?.FaviconUrl,
            PrimaryColor = custom?.PrimaryColor,
            SecondaryColor = custom?.SecondaryColor,
            BackgroundColor = custom?.BackgroundColor,
            TextColor = custom?.TextColor,
            FontFamily = custom?.FontFamily,
            CustomCss = custom?.CustomCss,
            CustomJs = custom?.CustomJs,
            Pages = pages,
            Features = features,
            Categories = categories,
            MenuItems = menuItems,
            BusinessHours = company?.BusinessHours is { Count: > 0 }
                ? new Dictionary<string, string>(company.BusinessHours, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
    }

    internal Task<string> GenerateHtmlAsync(
        WebsiteTenantData tenant,
        WebsiteTemplate template,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var enc = HtmlEncoder.Default;
        var displayName = enc.Encode(tenant.CompanyName ?? tenant.Name);
        var description = enc.Encode(
            string.IsNullOrWhiteSpace(tenant.Description)
                ? $"{tenant.CompanyName ?? tenant.Name} — powered by Regkasse"
                : tenant.Description!);
        var address = enc.Encode(tenant.Address ?? string.Empty);
        var phone = enc.Encode(tenant.Phone ?? string.Empty);
        var email = enc.Encode(tenant.Email ?? string.Empty);
        var hoursHtml = BuildBusinessHoursHtml(tenant.BusinessHours, enc);
        var logoSrc = string.IsNullOrWhiteSpace(tenant.LogoUrl)
            ? DefaultLogoRelativePath
            : tenant.LogoUrl!;
        var logoHtml = $"<img class=\"logo\" src=\"{enc.Encode(logoSrc)}\" alt=\"{displayName}\" />";
        var faviconHtml = string.IsNullOrWhiteSpace(tenant.FaviconUrl)
            ? string.Empty
            : $"<link rel=\"icon\" href=\"{enc.Encode(tenant.FaviconUrl)}\" />";

        var navHtml = BuildPagesNavHtml(tenant.Pages, enc);
        var menuHtml = BuildMenuHtml(tenant, enc);

        var html = $"""
            <!DOCTYPE html>
            <html lang="de">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>{displayName}</title>
              <meta name="description" content="{description}" />
              {faviconHtml}
              <link rel="stylesheet" href="styles.css" />
            </head>
            <body data-template="{enc.Encode(template.Id)}" class="template-{enc.Encode(template.Id)}">
              <header class="hero" id="home">
                {logoHtml}
                <h1>{displayName}</h1>
                <p class="tagline">{description}</p>
                {navHtml}
              </header>
              <main>
                {menuHtml}
                <section class="contact" id="contact" aria-label="Kontakt">
                  <h2>Kontakt</h2>
                  {(string.IsNullOrEmpty(address) ? "" : $"<p class=\"address\">{address}</p>")}
                  {(string.IsNullOrEmpty(phone) ? "" : $"<p class=\"phone\"><a href=\"tel:{phone}\">{phone}</a></p>")}
                  {(string.IsNullOrEmpty(email) ? "" : $"<p class=\"email\"><a href=\"mailto:{email}\">{email}</a></p>")}
                </section>
                <section class="hours" id="about" aria-label="Öffnungszeiten">
                  <h2>Öffnungszeiten</h2>
                  {hoursHtml}
                </section>
              </main>
              <footer>
                <p>{displayName}</p>
              </footer>
              <script src="app.js" defer></script>
            </body>
            </html>
            """;

        return Task.FromResult(html);
    }

    internal Task<string> GenerateCssAsync(
        WebsiteTenantData tenant,
        WebsiteTemplate template,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var css = template.Id.ToLowerInvariant() switch
        {
            "classic" => """
                :root { --bg: #f7f1e8; --fg: #2c1810; --accent: #8b4513; --muted: #6b5344; }
                * { box-sizing: border-box; }
                body { margin: 0; font-family: Georgia, "Times New Roman", serif; background: var(--bg); color: var(--fg); line-height: 1.6; }
                .hero { padding: 3rem 1.5rem; text-align: center; background: linear-gradient(180deg, #efe2d0, var(--bg)); border-bottom: 3px double var(--accent); }
                .logo { max-height: 96px; margin-bottom: 1rem; }
                h1 { font-size: 2.4rem; margin: 0 0 0.5rem; letter-spacing: 0.02em; }
                .tagline { color: var(--muted); max-width: 36rem; margin: 0 auto; }
                main { max-width: 40rem; margin: 0 auto; padding: 2rem 1.5rem; }
                h2 { color: var(--accent); border-bottom: 1px solid #d4c4b0; padding-bottom: 0.35rem; }
                a { color: var(--accent); }
                footer { text-align: center; padding: 2rem; color: var(--muted); font-size: 0.9rem; }
                .hours-list { list-style: none; padding: 0; }
                .hours-list li { display: flex; justify-content: space-between; gap: 1rem; padding: 0.25rem 0; }
                .site-nav { display: flex; flex-wrap: wrap; gap: 0.75rem; justify-content: center; margin-top: 1rem; }
                .site-nav a { text-decoration: none; }
                .menu-list { list-style: none; padding: 0; }
                .menu-list li { display: flex; justify-content: space-between; gap: 1rem; padding: 0.35rem 0; }
                """,
            "minimal" => """
                :root { --bg: #ffffff; --fg: #111111; --accent: #111111; --muted: #666666; }
                * { box-sizing: border-box; }
                body { margin: 0; font-family: system-ui, -apple-system, Segoe UI, sans-serif; background: var(--bg); color: var(--fg); line-height: 1.5; }
                .hero { padding: 4rem 1.25rem 2rem; text-align: left; max-width: 36rem; margin: 0 auto; }
                .logo { max-height: 64px; margin-bottom: 1.5rem; }
                h1 { font-size: 2rem; font-weight: 600; margin: 0 0 0.75rem; letter-spacing: -0.02em; }
                .tagline { color: var(--muted); margin: 0; }
                main { max-width: 36rem; margin: 0 auto; padding: 0 1.25rem 3rem; }
                h2 { font-size: 0.85rem; text-transform: uppercase; letter-spacing: 0.08em; color: var(--muted); font-weight: 600; }
                a { color: var(--accent); text-decoration: underline; text-underline-offset: 3px; }
                footer { max-width: 36rem; margin: 0 auto; padding: 0 1.25rem 3rem; color: var(--muted); font-size: 0.85rem; }
                .hours-list { list-style: none; padding: 0; }
                .hours-list li { display: flex; justify-content: space-between; gap: 1rem; padding: 0.35rem 0; border-bottom: 1px solid #eee; }
                .site-nav { display: flex; flex-wrap: wrap; gap: 0.75rem; margin-top: 1rem; }
                .site-nav a { text-decoration: none; }
                .menu-list { list-style: none; padding: 0; }
                .menu-list li { display: flex; justify-content: space-between; gap: 1rem; padding: 0.4rem 0; border-bottom: 1px solid #eee; }
                """,
            _ => """
                :root { --bg: #0f172a; --fg: #f8fafc; --accent: #38bdf8; --muted: #94a3b8; --card: #1e293b; }
                * { box-sizing: border-box; }
                body { margin: 0; font-family: "Segoe UI", system-ui, sans-serif; background: radial-gradient(circle at top, #1e293b, var(--bg)); color: var(--fg); line-height: 1.6; min-height: 100vh; }
                .hero { padding: 4rem 1.5rem 2rem; text-align: center; }
                .logo { max-height: 88px; margin-bottom: 1.25rem; border-radius: 12px; }
                h1 { font-size: clamp(2rem, 4vw, 3rem); margin: 0 0 0.75rem; font-weight: 700; }
                .tagline { color: var(--muted); max-width: 34rem; margin: 0 auto; }
                main { max-width: 42rem; margin: 0 auto; padding: 1rem 1.5rem 3rem; display: grid; gap: 1.25rem; }
                section { background: color-mix(in srgb, var(--card) 90%, transparent); border: 1px solid #334155; border-radius: 16px; padding: 1.25rem 1.5rem; }
                h2 { margin-top: 0; color: var(--accent); font-size: 1.1rem; }
                a { color: var(--accent); }
                footer { text-align: center; padding: 2rem; color: var(--muted); font-size: 0.9rem; }
                .hours-list { list-style: none; padding: 0; margin: 0; }
                .hours-list li { display: flex; justify-content: space-between; gap: 1rem; padding: 0.4rem 0; border-bottom: 1px solid #334155; }
                .hours-list li:last-child { border-bottom: none; }
                .site-nav { display: flex; flex-wrap: wrap; gap: 0.75rem; justify-content: center; margin-top: 1rem; }
                .site-nav a { text-decoration: none; }
                .menu-list { list-style: none; padding: 0; margin: 0; }
                .menu-list li { display: flex; justify-content: space-between; gap: 1rem; padding: 0.4rem 0; border-bottom: 1px solid #334155; }
                .menu-list li:last-child { border-bottom: none; }
                """
        };

        css = ApplyThemeOverrides(css, tenant);
        if (!string.IsNullOrWhiteSpace(tenant.CustomCss))
            css += "\n/* tenant custom */\n" + tenant.CustomCss;

        return Task.FromResult(css);
    }

    internal Task<string> GenerateJsAsync(
        WebsiteTenantData tenant,
        WebsiteTemplate template,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var payload = JsonSerializer.Serialize(new
        {
            tenantId = tenant.TenantId,
            slug = tenant.Slug,
            templateId = template.Id,
            name = tenant.CompanyName ?? tenant.Name,
            pages = tenant.Pages,
            features = tenant.Features
        });

        var js = $$"""
            (function () {
              var meta = {{payload}};
              document.documentElement.dataset.siteReady = "1";
              if (window.console && console.info) {
                console.info("Regkasse site ready", meta.slug, meta.templateId);
              }
            })();
            """;

        if (!string.IsNullOrWhiteSpace(tenant.CustomJs))
            js += "\n/* tenant custom */\n" + tenant.CustomJs;

        return Task.FromResult(js);
    }

    internal async Task<string> DeployToCdnAsync(
        string slug,
        string html,
        string css,
        string js,
        CancellationToken ct)
    {
        var opts = _options.Value;
        var root = Path.Combine(_environment.ContentRootPath, opts.RootRelativeDirectory);
        var siteDir = Path.Combine(root, slug);
        Directory.CreateDirectory(siteDir);

        var htmlPath = Path.Combine(siteDir, "index.html");
        var cssPath = Path.Combine(siteDir, "styles.css");
        var jsPath = Path.Combine(siteDir, "app.js");

        await File.WriteAllTextAsync(htmlPath, html, Encoding.UTF8, ct);
        await File.WriteAllTextAsync(cssPath, css, Encoding.UTF8, ct);
        await File.WriteAllTextAsync(jsPath, js, Encoding.UTF8, ct);
        await EnsureDefaultLogoAsync(siteDir, ct);

        var prefix = opts.PublicUrlPathPrefix.TrimEnd('/');
        if (!prefix.StartsWith('/'))
            prefix = "/" + prefix;

        var relative = $"{prefix}/{slug}/";
        if (!string.IsNullOrWhiteSpace(opts.PublicBaseUrl))
        {
            var origin = opts.PublicBaseUrl.TrimEnd('/');
            return origin + relative;
        }

        return relative;
    }

    internal static bool IsSafeSlug(string slug) =>
        !string.IsNullOrWhiteSpace(slug) && SafeSlugRegex.IsMatch(slug);

    internal static async Task EnsureDefaultLogoAsync(string siteDir, CancellationToken ct)
    {
        var logoPath = Path.Combine(siteDir, DefaultLogoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var parent = Path.GetDirectoryName(logoPath);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);
        if (!File.Exists(logoPath))
            await File.WriteAllTextAsync(logoPath, DefaultLogoSvg, Encoding.UTF8, ct);
    }

    private static string ApplyThemeOverrides(string css, WebsiteTenantData tenant)
    {
        if (!string.IsNullOrWhiteSpace(tenant.BackgroundColor))
            css = Regex.Replace(css, @"--bg:\s*#[0-9a-fA-F]{3,8}", $"--bg: {tenant.BackgroundColor}");
        if (!string.IsNullOrWhiteSpace(tenant.TextColor))
            css = Regex.Replace(css, @"--fg:\s*#[0-9a-fA-F]{3,8}", $"--fg: {tenant.TextColor}");
        if (!string.IsNullOrWhiteSpace(tenant.SecondaryColor ?? tenant.PrimaryColor))
        {
            var accent = tenant.SecondaryColor ?? tenant.PrimaryColor!;
            css = Regex.Replace(css, @"--accent:\s*#[0-9a-fA-F]{3,8}", $"--accent: {accent}");
        }

        if (!string.IsNullOrWhiteSpace(tenant.FontFamily))
            css += $"\nbody {{ font-family: {tenant.FontFamily}; }}\n";

        return css;
    }

    private static string BuildMenuHtml(WebsiteTenantData tenant, HtmlEncoder enc)
    {
        if (!tenant.HasLiveMenu)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("""
            <section class="menu" id="menu" aria-label="Speisekarte">
              <h2>Speisekarte</h2>
            """);

        if (tenant.MenuItems.Count == 0)
        {
            sb.AppendLine("<p>Speisekarte wird aktualisiert.</p>");
            sb.AppendLine("</section>");
            return sb.ToString();
        }

        foreach (var group in tenant.MenuItems.GroupBy(i => i.CategoryName ?? "Sonstiges"))
        {
            sb.Append(CultureInfo.InvariantCulture, $"<h3>{enc.Encode(group.Key)}</h3>");
            sb.AppendLine("<ul class=\"menu-list\">");
            foreach (var item in group)
            {
                var price = item.Price.ToString("0.00", CultureInfo.InvariantCulture);
                sb.Append(CultureInfo.InvariantCulture,
                    $"<li><span>{enc.Encode(item.Name)}</span><span>{price} €</span></li>");
            }

            sb.AppendLine("</ul>");
        }

        sb.AppendLine("</section>");
        return sb.ToString();
    }

    private static string BuildPagesNavHtml(IReadOnlyList<string> pages, HtmlEncoder enc)
    {
        if (pages.Count == 0)
            return string.Empty;

        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = "Start",
            ["menu"] = "Speisekarte",
            ["about"] = "Über uns",
            ["contact"] = "Kontakt",
            ["gallery"] = "Galerie",
            ["reservation"] = "Reservierung"
        };

        var sb = new StringBuilder();
        sb.Append("<nav class=\"site-nav\" aria-label=\"Seiten\">");
        foreach (var page in pages)
        {
            var label = labels.TryGetValue(page, out var l) ? l : page;
            sb.Append(CultureInfo.InvariantCulture, $"<a href=\"#{enc.Encode(page)}\">{enc.Encode(label)}</a>");
        }

        sb.Append("</nav>");
        return sb.ToString();
    }

    private static string BuildBusinessHoursHtml(
        IReadOnlyDictionary<string, string> hours,
        HtmlEncoder enc)
    {
        if (hours.Count == 0)
            return "<p>Öffnungszeiten auf Anfrage</p>";

        var sb = new StringBuilder();
        sb.Append("<ul class=\"hours-list\">");
        foreach (var pair in hours.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append(CultureInfo.InvariantCulture, $"<li><span>{enc.Encode(pair.Key)}</span><span>{enc.Encode(pair.Value)}</span></li>");
        }

        sb.Append("</ul>");
        return sb.ToString();
    }

    private readonly record struct GenerationResolveResult(
        bool Succeeded,
        WebsiteTenantData? Tenant,
        WebsiteTemplate? Template,
        string? Code,
        string? Error)
    {
        public static GenerationResolveResult Ok(WebsiteTenantData tenant, WebsiteTemplate template) =>
            new(true, tenant, template, null, null);

        public static GenerationResolveResult Fail(string code, string error) =>
            new(false, null, null, code, error);
    }
}
