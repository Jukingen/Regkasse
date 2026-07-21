using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Website;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.App;

/// <summary>
/// One-click mobile app generator from FA tenant catalog + company settings.
/// PWA: publishes installable static files. Native: publishes an Expo source ZIP (no on-host compile).
/// </summary>
public sealed class AppGeneratorService : IAppGeneratorService
{
    public const string DisabledCode = "APP_GENERATOR_DISABLED";
    public const string TenantNotFoundCode = "TENANT_NOT_FOUND";
    public const string InvalidSlugCode = "INVALID_TENANT_SLUG";
    public const string UnsupportedAppTypeCode = "UNSUPPORTED_APP_TYPE";
    public const string DeployFailedCode = "APP_DEPLOY_FAILED";
    public const string DefaultLogoRelativePath = "assets/default-logo.svg";

    internal static readonly string DefaultLogoSvg = WebsiteGeneratorService.DefaultLogoSvg;

    private static readonly Regex SafeSlugRegex = new(
        @"^[a-z0-9]([a-z0-9-]{0,62}[a-z0-9])?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IOptions<WebsiteGeneratorOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly ITenantCustomizationService? _customizations;
    private readonly ILogger<AppGeneratorService> _logger;

    public AppGeneratorService(
        IDbContextFactory<AppDbContext> dbFactory,
        IOptions<WebsiteGeneratorOptions> options,
        IHostEnvironment environment,
        ILogger<AppGeneratorService> logger,
        ITenantCustomizationService? customizations = null)
    {
        _dbFactory = dbFactory;
        _options = options;
        _environment = environment;
        _logger = logger;
        _customizations = customizations;
    }

    public async Task<AppResult> GenerateAppAsync(
        Guid tenantId,
        AppType appType,
        CancellationToken ct = default)
    {
        if (!_options.Value.Enabled)
            return AppResult.Fail(DisabledCode, "App generator is disabled.");

        if (appType is not (AppType.Pwa or AppType.Native))
            return AppResult.Fail(UnsupportedAppTypeCode, "Unsupported app type.");

        var tenant = await GetTenantDataAsync(tenantId, ct);
        if (tenant is null)
            return AppResult.Fail(TenantNotFoundCode, "Tenant not found.");

        if (!SafeSlugRegex.IsMatch(tenant.Slug))
            return AppResult.Fail(InvalidSlugCode, "Tenant slug is not valid for public hosting.");

        try
        {
            var config = new AppConfig
            {
                TenantId = tenant.Id,
                AppName = tenant.DisplayName,
                Slug = tenant.Slug,
                Colors = await GetColorsAsync(tenantId, ct),
                Logo = await GetLogoAsync(tenantId, ct) ?? DefaultLogoRelativePath,
                Menu = await GetMenuAsync(tenantId, ct),
                Categories = await GetCategoriesAsync(tenantId, ct),
                Description = tenant.Description,
                Phone = tenant.Phone,
                Email = tenant.Email,
                Address = tenant.Address,
                LiveMenuPath = $"/api/public/tenants/{tenant.Slug}/menu"
            };

            var appSource = await GenerateAppSourceAsync(config, appType, ct);
            var appFile = await BuildAppAsync(appSource, appType, ct);
            var downloadUrl = await DeployAppAsync(appFile, tenant.Slug, appType, ct);
            return AppResult.Success(downloadUrl, appType);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "App generation failed for tenant {TenantId} type {AppType}",
                tenantId,
                appType);
            return AppResult.Fail(DeployFailedCode, "App generation failed.");
        }
    }

    internal async Task<AppTenantSnapshot?> GetTenantDataAsync(Guid tenantId, CancellationToken ct)
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

        return new AppTenantSnapshot
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            DisplayName = company?.CompanyName ?? tenant.Name,
            Description = company?.CompanyDescription,
            Phone = company?.CompanyPhone ?? tenant.Phone,
            Email = company?.CompanyEmail ?? tenant.Email,
            Address = company?.CompanyAddress ?? tenant.Address,
            Logo = company?.CompanyLogo
        };
    }

    internal async Task<AppColorPalette> GetColorsAsync(Guid tenantId, CancellationToken ct)
    {
        if (_customizations is not null)
        {
            var custom = await _customizations.GetAsync(tenantId, TenantCustomization.TypeApp, ct);
            if (custom is not null
                && (!string.IsNullOrWhiteSpace(custom.PrimaryColor)
                    || !string.IsNullOrWhiteSpace(custom.SecondaryColor)
                    || !string.IsNullOrWhiteSpace(custom.BackgroundColor)
                    || !string.IsNullOrWhiteSpace(custom.TextColor)))
            {
                return new AppColorPalette
                {
                    Primary = custom.PrimaryColor ?? "#0f172a",
                    Accent = custom.SecondaryColor ?? "#38bdf8",
                    Background = custom.BackgroundColor ?? "#ffffff",
                    Text = custom.TextColor ?? "#0f172a"
                };
            }
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var categoryColor = await db.Categories.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.IsActive && c.Color != null && c.Color != "")
            .OrderBy(c => c.SortOrder)
            .Select(c => c.Color)
            .FirstOrDefaultAsync(ct);

        var primary = NormalizeHex(categoryColor) ?? "#0f172a";
        return new AppColorPalette
        {
            Primary = primary,
            Accent = "#38bdf8",
            Background = "#ffffff",
            Text = "#0f172a"
        };
    }

    internal async Task<string?> GetLogoAsync(Guid tenantId, CancellationToken ct)
    {
        if (_customizations is not null)
        {
            var custom = await _customizations.GetAsync(tenantId, TenantCustomization.TypeApp, ct);
            if (!string.IsNullOrWhiteSpace(custom?.LogoUrl))
                return custom.LogoUrl;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.CompanySettings.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .Select(c => c.CompanyLogo)
            .FirstOrDefaultAsync(ct);
    }

    internal async Task<IReadOnlyList<AppMenuItem>> GetMenuAsync(Guid tenantId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Products.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .OrderBy(p => p.Name)
            .Take(200)
            .Select(p => new AppMenuItem
            {
                Id = p.Id,
                Name = p.Name,
                CategoryId = p.CategoryId,
                CategoryName = p.CategoryNavigation != null ? p.CategoryNavigation.Name : p.Category,
                Price = p.Price,
                ImageUrl = p.ImageUrl,
                Description = p.Description
            })
            .ToListAsync(ct);
    }

    internal async Task<IReadOnlyList<AppCategoryItem>> GetCategoriesAsync(Guid tenantId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Categories.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new AppCategoryItem
            {
                Id = c.Id,
                Key = c.Key,
                Name = c.Name,
                Color = c.Color,
                Icon = c.Icon,
                SortOrder = c.SortOrder
            })
            .ToListAsync(ct);
    }

    internal Task<AppSourceBundle> GenerateAppSourceAsync(
        AppConfig config,
        AppType appType,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        var configJson = JsonSerializer.Serialize(config, JsonOptions);
        files["config.json"] = Encoding.UTF8.GetBytes(configJson);

        if (appType == AppType.Pwa)
        {
            foreach (var (name, content) in BuildPwaFiles(config))
                files[name] = Encoding.UTF8.GetBytes(content);
        }
        else
        {
            foreach (var (name, content) in BuildNativeFiles(config))
                files[name] = Encoding.UTF8.GetBytes(content);
        }

        return Task.FromResult(new AppSourceBundle(appType, files));
    }

    internal Task<BuiltAppArtifact> BuildAppAsync(
        AppSourceBundle appSource,
        AppType appType,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (appType == AppType.Pwa)
        {
            return Task.FromResult(new BuiltAppArtifact(
                appType,
                EntryFileName: "index.html",
                Files: appSource.Files,
                IsZip: false));
        }

        // Native: package Expo/RN source as a ZIP download (API host does not run eas/build).
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, bytes) in appSource.Files)
            {
                var entry = zip.CreateEntry(path.Replace('\\', '/'), CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                entryStream.Write(bytes);
            }
        }

        return Task.FromResult(new BuiltAppArtifact(
            appType,
            EntryFileName: "app-source.zip",
            Files: new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["app-source.zip"] = ms.ToArray()
            },
            IsZip: true));
    }

    internal async Task<string> DeployAppAsync(
        BuiltAppArtifact appFile,
        string slug,
        AppType appType,
        CancellationToken ct)
    {
        var opts = _options.Value;
        var root = Path.Combine(_environment.ContentRootPath, opts.RootRelativeDirectory);
        var relativeSegment = appType == AppType.Pwa ? "app" : "app-native";
        var deployDir = Path.Combine(root, slug, relativeSegment);
        Directory.CreateDirectory(deployDir);

        foreach (var (name, bytes) in appFile.Files)
        {
            var target = Path.Combine(deployDir, name.Replace('/', Path.DirectorySeparatorChar));
            var parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);
            await File.WriteAllBytesAsync(target, bytes, ct);
        }

        if (appType == AppType.Pwa)
            await EnsureDefaultLogoAsync(deployDir, ct);

        var prefix = opts.PublicUrlPathPrefix.TrimEnd('/');
        if (!prefix.StartsWith('/'))
            prefix = "/" + prefix;

        var relative = appType == AppType.Pwa
            ? $"{prefix}/{slug}/app/"
            : $"{prefix}/{slug}/app-native/{appFile.EntryFileName}";

        if (!string.IsNullOrWhiteSpace(opts.PublicBaseUrl))
            return opts.PublicBaseUrl.TrimEnd('/') + relative;

        return relative;
    }

    private static IEnumerable<(string Name, string Content)> BuildPwaFiles(AppConfig config)
    {
        var enc = HtmlEncoder.Default;
        var name = enc.Encode(config.AppName);
        var desc = enc.Encode(config.Description ?? $"{config.AppName} — Regkasse App");
        var primary = enc.Encode(config.Colors.Primary);
        var accent = enc.Encode(config.Colors.Accent);
        var logoHtml = $"<img class=\"logo\" src=\"{enc.Encode(config.Logo ?? DefaultLogoRelativePath)}\" alt=\"{name}\" />";

        var categoriesHtml = new StringBuilder();
        foreach (var cat in config.Categories)
        {
            categoriesHtml.Append(
                $"<li data-category-id=\"{cat.Id}\"><span>{enc.Encode(cat.Name)}</span></li>");
        }

        if (categoriesHtml.Length == 0)
            categoriesHtml.Append("<li>Keine Kategorien</li>");

        var menuHtml = new StringBuilder();
        foreach (var item in config.Menu.Take(50))
        {
            menuHtml.Append(
                $"<li><strong>{enc.Encode(item.Name)}</strong> <span>{item.Price:0.00} €</span></li>");
        }

        if (menuHtml.Length == 0)
            menuHtml.Append("<li>Keine Produkte</li>");

        var manifest = JsonSerializer.Serialize(new
        {
            name = config.AppName,
            short_name = config.AppName.Length > 12 ? config.AppName[..12] : config.AppName,
            description = config.Description ?? config.AppName,
            start_url = "./",
            display = "standalone",
            background_color = config.Colors.Background,
            theme_color = config.Colors.Primary,
            lang = "de",
            icons = Array.Empty<object>()
        }, JsonOptions);

        var html = $"""
            <!DOCTYPE html>
            <html lang="de">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover" />
              <meta name="theme-color" content="{primary}" />
              <meta name="apple-mobile-web-app-capable" content="yes" />
              <title>{name}</title>
              <link rel="manifest" href="manifest.webmanifest" />
              <link rel="stylesheet" href="styles.css" />
            </head>
            <body>
              <main class="shell">
                {logoHtml}
                <h1>{name}</h1>
                <p>{desc}</p>
                <section>
                  <h2>Kategorien</h2>
                  <ul class="categories">{categoriesHtml}</ul>
                </section>
                <section>
                  <h2>Menü</h2>
                  <ul class="menu" id="menu-list">{menuHtml}</ul>
                </section>
                <p class="hint">Zum Homescreen hinzufügen, um die App zu installieren.</p>
              </main>
              <script src="sw-register.js" defer></script>
              <script src="menu-live.js" defer></script>
            </body>
            </html>
            """;

        var css = $$"""
            body { margin: 0; font-family: system-ui, sans-serif; background: {{config.Colors.Background}}; color: {{config.Colors.Text}}; min-height: 100vh; }
            .shell { max-width: 28rem; margin: 0 auto; padding: 2rem 1.25rem; }
            .logo { max-height: 80px; margin-bottom: 1rem; border-radius: 16px; }
            h1 { font-size: 1.75rem; margin: 0 0 0.75rem; color: {{primary}}; }
            h2 { font-size: 1rem; color: {{accent}}; }
            ul { list-style: none; padding: 0; }
            li { display: flex; justify-content: space-between; gap: 0.75rem; padding: 0.4rem 0; border-bottom: 1px solid #e2e8f0; }
            .hint { margin-top: 2rem; opacity: 0.8; font-size: 0.9rem; }
            """;

        var sw = """
            self.addEventListener("install", function (event) {
              event.waitUntil(caches.open("regkasse-app-v1").then(function (cache) {
                return cache.addAll(["./", "./index.html", "./styles.css", "./manifest.webmanifest", "./config.json"]);
              }));
              self.skipWaiting();
            });
            self.addEventListener("fetch", function (event) {
              event.respondWith(
                caches.match(event.request).then(function (cached) {
                  return cached || fetch(event.request);
                })
              );
            });
            """;

        var register = """
            if ("serviceWorker" in navigator) {
              navigator.serviceWorker.register("./sw.js").catch(function () {});
            }
            """;

        // Shared platform: refresh menu from the same public API used by frontend-sites.
        var menuLive = """
            (function () {
              function euro(n) {
                try { return new Intl.NumberFormat("de-AT", { style: "currency", currency: "EUR" }).format(n); }
                catch (e) { return (Number(n) || 0).toFixed(2) + " €"; }
              }
              fetch("./config.json")
                .then(function (r) { return r.json(); })
                .then(function (cfg) {
                  if (!cfg || !cfg.liveMenuPath) return null;
                  return fetch(cfg.liveMenuPath).then(function (r) {
                    if (!r.ok) throw new Error("menu");
                    return r.json();
                  });
                })
                .then(function (menu) {
                  if (!menu || !menu.items) return;
                  var list = document.getElementById("menu-list");
                  if (!list) return;
                  list.innerHTML = "";
                  (menu.items || []).slice(0, 50).forEach(function (item) {
                    var li = document.createElement("li");
                    var strong = document.createElement("strong");
                    strong.textContent = item.name || "";
                    var span = document.createElement("span");
                    span.textContent = euro(item.price);
                    li.appendChild(strong);
                    li.appendChild(span);
                    list.appendChild(li);
                  });
                })
                .catch(function () {});
            })();
            """;

        yield return ("index.html", html);
        yield return ("styles.css", css);
        yield return ("manifest.webmanifest", manifest);
        yield return ("sw.js", sw);
        yield return ("sw-register.js", register);
        yield return ("menu-live.js", menuLive);
    }

    private static IEnumerable<(string Name, string Content)> BuildNativeFiles(AppConfig config)
    {
        var appJson = JsonSerializer.Serialize(new
        {
            expo = new
            {
                name = config.AppName,
                slug = config.Slug,
                version = "1.0.0",
                orientation = "portrait",
                userInterfaceStyle = "light",
                splash = new { backgroundColor = config.Colors.Primary },
                android = new { package = $"at.regkasse.{SanitizePackageSegment(config.Slug)}" },
                ios = new { bundleIdentifier = $"at.regkasse.{SanitizePackageSegment(config.Slug)}" },
                extra = new
                {
                    tenantId = config.TenantId,
                    primaryColor = config.Colors.Primary,
                    accentColor = config.Colors.Accent
                }
            }
        }, JsonOptions);

        var appTsx = """
            import React from 'react';
            import { SafeAreaView, ScrollView, Text, View } from 'react-native';
            import config from './config.json';

            export default function App() {
              return (
                <SafeAreaView style={{ flex: 1, backgroundColor: config.colors.background }}>
                  <ScrollView contentContainerStyle={{ padding: 20 }}>
                    <Text style={{ fontSize: 28, fontWeight: '700', color: config.colors.primary }}>
                      {config.appName}
                    </Text>
                    <Text style={{ marginTop: 8, color: config.colors.text }}>
                      {config.description || 'Regkasse App'}
                    </Text>
                    <Text style={{ marginTop: 24, fontWeight: '600' }}>Kategorien</Text>
                    {(config.categories || []).map((c) => (
                      <Text key={c.id} style={{ marginTop: 6 }}>{c.name}</Text>
                    ))}
                    <Text style={{ marginTop: 24, fontWeight: '600' }}>Menü</Text>
                    {(config.menu || []).map((item) => (
                      <View key={item.id} style={{ marginTop: 8, flexDirection: 'row', justifyContent: 'space-between' }}>
                        <Text>{item.name}</Text>
                        <Text>{Number(item.price).toFixed(2)} €</Text>
                      </View>
                    ))}
                  </ScrollView>
                </SafeAreaView>
              );
            }
            """;

        var packageJson = JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["name"] = $"regkasse-app-{config.Slug}",
                ["version"] = "1.0.0",
                ["private"] = true,
                ["main"] = "expo/AppEntry.js",
                ["scripts"] = new Dictionary<string, string>
                {
                    ["start"] = "expo start",
                    ["android"] = "expo start --android",
                    ["ios"] = "expo start --ios"
                },
                ["dependencies"] = new Dictionary<string, string>
                {
                    ["expo"] = "~52.0.0",
                    ["react"] = "18.3.1",
                    ["react-native"] = "0.76.0"
                }
            },
            JsonOptions);

        var readme = $"""
            # {config.AppName} (Regkasse Native package)

            Generated from tenant `{config.Slug}`.

            ## Run locally
            1. `npm install`
            2. `npx expo start`

            This ZIP is a source package. Production store builds use EAS / local native tooling — the API host does not compile IPA/APK.
            """;

        yield return ("app.json", appJson);
        yield return ("App.tsx", appTsx);
        yield return ("package.json", packageJson);
        yield return ("README.md", readme);
    }

    private static async Task EnsureDefaultLogoAsync(string deployDir, CancellationToken ct)
    {
        var logoPath = Path.Combine(deployDir, DefaultLogoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var parent = Path.GetDirectoryName(logoPath);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);
        if (!File.Exists(logoPath))
            await File.WriteAllTextAsync(logoPath, DefaultLogoSvg, Encoding.UTF8, ct);
    }

    private static string? NormalizeHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var v = value.Trim();
        if (Regex.IsMatch(v, @"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$"))
            return v.Length == 4
                ? $"#{v[1]}{v[1]}{v[2]}{v[2]}{v[3]}{v[3]}"
                : v;
        return null;
    }

    private static string SanitizePackageSegment(string slug) =>
        Regex.Replace(slug, @"[^a-z0-9]", "", RegexOptions.IgnoreCase);

    internal sealed class AppTenantSnapshot
    {
        public Guid Id { get; init; }
        public required string Name { get; init; }
        public required string Slug { get; init; }
        public required string DisplayName { get; init; }
        public string? Description { get; init; }
        public string? Phone { get; init; }
        public string? Email { get; init; }
        public string? Address { get; init; }
        public string? Logo { get; init; }
    }

    internal sealed record AppSourceBundle(
        AppType AppType,
        IReadOnlyDictionary<string, byte[]> Files);

    internal sealed record BuiltAppArtifact(
        AppType AppType,
        string EntryFileName,
        IReadOnlyDictionary<string, byte[]> Files,
        bool IsZip);
}
