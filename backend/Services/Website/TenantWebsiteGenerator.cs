using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Website;

/// <summary>
/// Builds a downloadable static-site ZIP for a tenant custom domain
/// (HTML/CSS/JS + optional logo + deploy script). Reuses
/// <see cref="WebsiteGeneratorService"/> templates — does not invent a parallel renderer.
/// </summary>
public sealed class TenantWebsiteGenerator : ITenantWebsiteGenerator
{
    public const string DisabledCode = "WEBSITE_GENERATOR_DISABLED";
    public const string TenantNotFoundCode = "TENANT_NOT_FOUND";
    public const string TemplateNotFoundCode = "TEMPLATE_NOT_FOUND";
    public const string InvalidDomainCode = "INVALID_DOMAIN";
    public const string InvalidSlugCode = "INVALID_TENANT_SLUG";
    public const string GenerateFailedCode = "WEBSITE_PACKAGE_FAILED";

    private static readonly Regex DomainRegex = new(
        @"^(?=.{1,253}$)(?!-)[a-z0-9-]{1,63}(?<!-)(\.(?!-)[a-z0-9-]{1,63}(?<!-))+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly WebsiteGeneratorService _website;
    private readonly IOptions<WebsiteGeneratorOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly ILogger<TenantWebsiteGenerator> _logger;

    public TenantWebsiteGenerator(
        WebsiteGeneratorService website,
        IOptions<WebsiteGeneratorOptions> options,
        IHostEnvironment environment,
        ILogger<TenantWebsiteGenerator> logger,
        IHttpClientFactory? httpClientFactory = null)
    {
        _website = website;
        _options = options;
        _environment = environment;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<TenantWebsitePackageResult> GenerateWebsiteAsync(
        Guid tenantId,
        string domain,
        string? templateId = null,
        CancellationToken ct = default)
    {
        var opts = _options.Value;
        if (!opts.Enabled)
            return TenantWebsitePackageResult.Fail(DisabledCode, "Website generator is disabled.");

        var normalizedDomain = NormalizeDomain(domain);
        if (normalizedDomain is null)
            return TenantWebsitePackageResult.Fail(InvalidDomainCode, "Domain is invalid.");

        var templateKey = string.IsNullOrWhiteSpace(templateId) ? "modern" : templateId.Trim();
        var template = _website.GetTemplates()
            .FirstOrDefault(t => string.Equals(t.Id, templateKey, StringComparison.OrdinalIgnoreCase));
        if (template is null)
            return TenantWebsitePackageResult.Fail(TemplateNotFoundCode, "Template not found.");

        var tenant = await _website.GetTenantDataAsync(tenantId, ct);
        if (tenant is null)
            return TenantWebsitePackageResult.Fail(TenantNotFoundCode, "Tenant not found.");

        if (!WebsiteGeneratorService.IsSafeSlug(tenant.Slug))
            return TenantWebsitePackageResult.Fail(InvalidSlugCode, "Tenant slug is not valid for public hosting.");

        try
        {
            var logo = await TryGetLogoAsync(tenant, ct);
            var tenantForHtml = logo is { Length: > 0 }
                ? CloneWithLocalLogo(tenant, "logo.png")
                : string.Equals(tenant.LogoUrl, WebsiteGeneratorService.DefaultLogoRelativePath, StringComparison.Ordinal)
                    ? tenant
                    : CloneWithLocalLogo(tenant, WebsiteGeneratorService.DefaultLogoRelativePath);

            var html = await _website.GenerateHtmlAsync(tenantForHtml, template, ct);
            // Menu is already embedded by WebsiteGeneratorService when live-menu is enabled.

            var css = await _website.GenerateCssAsync(tenant, template, ct);
            var js = await _website.GenerateJsAsync(tenant, template, ct);

            var files = new List<GeneratedWebsiteFile>
            {
                new("index.html", Encoding.UTF8.GetBytes(html)),
                new("styles.css", Encoding.UTF8.GetBytes(css)),
                new("app.js", Encoding.UTF8.GetBytes(js))
            };

            if (logo is { Length: > 0 })
            {
                files.Add(new GeneratedWebsiteFile("logo.png", logo));
            }
            else
            {
                files.Add(new GeneratedWebsiteFile(
                    WebsiteGeneratorService.DefaultLogoRelativePath,
                    Encoding.UTF8.GetBytes(WebsiteGeneratorService.DefaultLogoSvg)));
            }

            var script = GenerateDeploymentScript(normalizedDomain, tenant.Slug, opts.PublicUrlPathPrefix);
            files.Add(new GeneratedWebsiteFile("deploy.sh", Encoding.UTF8.GetBytes(script)));
            files.Add(new GeneratedWebsiteFile(
                "INSTRUCTIONS.txt",
                Encoding.UTF8.GetBytes(BuildInstructions(normalizedDomain, tenant.Slug, opts.PublicUrlPathPrefix))));

            // Also publish under the platform media path so FA preview stays available.
            var publishedUrl = await _website.DeployToCdnAsync(tenant.Slug, html, css, js, ct);

            var zip = CreateZip(files);
            var fileName = $"{tenant.Slug}-{normalizedDomain.Replace('.', '-')}-website.zip";

            return TenantWebsitePackageResult.Ok(
                zipFile: zip,
                script: script,
                instructions: BuildInstructions(normalizedDomain, tenant.Slug, opts.PublicUrlPathPrefix),
                fileName: fileName,
                domain: normalizedDomain,
                publishedUrl: publishedUrl,
                templateId: template.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Tenant website package failed for tenant {TenantId} domain {Domain}",
                tenantId,
                normalizedDomain);
            return TenantWebsitePackageResult.Fail(GenerateFailedCode, "Website package generation failed.");
        }
    }

    private static WebsiteTenantData CloneWithLocalLogo(WebsiteTenantData tenant, string logoUrl) =>
        new()
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
            LogoUrl = logoUrl,
            FaviconUrl = tenant.FaviconUrl,
            PrimaryColor = tenant.PrimaryColor,
            SecondaryColor = tenant.SecondaryColor,
            BackgroundColor = tenant.BackgroundColor,
            TextColor = tenant.TextColor,
            FontFamily = tenant.FontFamily,
            CustomCss = tenant.CustomCss,
            CustomJs = tenant.CustomJs,
            Pages = tenant.Pages,
            Features = tenant.Features,
            Categories = tenant.Categories,
            MenuItems = tenant.MenuItems,
            BusinessHours = tenant.BusinessHours
        };

    private async Task<byte[]?> TryGetLogoAsync(WebsiteTenantData tenant, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenant.LogoUrl)
            || string.Equals(tenant.LogoUrl, WebsiteGeneratorService.DefaultLogoRelativePath, StringComparison.Ordinal))
            return null;

        var url = tenant.LogoUrl.Trim();
        try
        {
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (_httpClientFactory is null)
                    return null;

                var client = _httpClientFactory.CreateClient(nameof(TenantWebsiteGenerator));
                using var response = await client.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                    return null;

                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                return bytes.Length is > 0 and < 2_000_000 ? bytes : null;
            }

            var relative = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var candidates = new[]
            {
                Path.Combine(_environment.ContentRootPath, relative),
                Path.Combine(_environment.ContentRootPath, "wwwroot", relative)
            };

            foreach (var path in candidates)
            {
                if (!File.Exists(path))
                    continue;
                var bytes = await File.ReadAllBytesAsync(path, ct);
                if (bytes.Length is > 0 and < 2_000_000)
                    return bytes;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Logo fetch skipped for tenant {TenantId}", tenant.TenantId);
        }

        return null;
    }

    private static byte[] CreateZip(IReadOnlyList<GeneratedWebsiteFile> files)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = zip.CreateEntry(file.Name.Replace('\\', '/'), CompressionLevel.Optimal);
                using var stream = entry.Open();
                stream.Write(file.Content);
            }
        }

        return ms.ToArray();
    }

    private static string GenerateDeploymentScript(string domain, string slug, string publicPrefix)
    {
        var prefix = publicPrefix.TrimEnd('/');
        if (!prefix.StartsWith('/'))
            prefix = "/" + prefix;

        return $$"""
            #!/usr/bin/env bash
            # Regkasse tenant website deploy helper for {{domain}}
            # Platform preview path: {{prefix}}/{{slug}}/
            set -euo pipefail

            TARGET_DIR="${1:-./site}"
            SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

            mkdir -p "$TARGET_DIR"
            cp -f "$SCRIPT_DIR/index.html" "$TARGET_DIR/"
            cp -f "$SCRIPT_DIR/styles.css" "$TARGET_DIR/"
            cp -f "$SCRIPT_DIR/app.js" "$TARGET_DIR/"
            if [[ -f "$SCRIPT_DIR/logo.png" ]]; then
              cp -f "$SCRIPT_DIR/logo.png" "$TARGET_DIR/"
            fi
            if [[ -d "$SCRIPT_DIR/assets" ]]; then
              mkdir -p "$TARGET_DIR/assets"
              cp -rf "$SCRIPT_DIR/assets/." "$TARGET_DIR/assets/"
            fi

            echo "Copied static site files to $TARGET_DIR"
            echo "Configure your reverse proxy / document root for {{domain}} to serve $TARGET_DIR"
            echo "Then point DNS A/CNAME for {{domain}} to this host."
            """;
    }

    private static string BuildInstructions(string domain, string slug, string publicPrefix)
    {
        var prefix = publicPrefix.TrimEnd('/');
        if (!prefix.StartsWith('/'))
            prefix = "/" + prefix;

        return $"""
            Regkasse website package for {domain}
            =====================================

            1. Download and extract this ZIP on your web host (or use the platform copy under {prefix}/{slug}/).
            2. Run: bash deploy.sh /path/to/document-root
               (or copy index.html, styles.css, app.js, optional logo.png / assets/ manually).
            3. Point DNS for {domain} (A or CNAME) to your server.
            4. Configure TLS (e.g. Let's Encrypt) for {domain}.

            Notes:
            - Use your host's configured document root (do not assume a fixed OS path).
            - On the Regkasse API host, generated sites are also published under App_Data/generated-websites/{slug}/.
            - Verify the custom domain in Admin → Website & App before relying on Host-based tenant routing.
            """;
    }

    private static string? NormalizeDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var host = value.Trim().ToLowerInvariant();
        if (host.StartsWith("http://", StringComparison.Ordinal) || host.StartsWith("https://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(host, UriKind.Absolute, out var uri))
                return null;
            host = uri.Host;
        }

        host = host.Trim().TrimEnd('.');
        if (host.StartsWith("www.", StringComparison.Ordinal))
            host = host[4..];

        return DomainRegex.IsMatch(host) ? host : null;
    }
}

/// <summary>One entry inside the generated website ZIP.</summary>
public sealed record GeneratedWebsiteFile(string Name, byte[] Content);

/// <summary>Outcome of <see cref="ITenantWebsiteGenerator.GenerateWebsiteAsync"/>.</summary>
public sealed class TenantWebsitePackageResult
{
    public bool Succeeded { get; private init; }
    public string? Code { get; private init; }
    public string? Error { get; private init; }
    public byte[]? ZipFile { get; private init; }
    public string? Script { get; private init; }
    public string? Instructions { get; private init; }
    public string? FileName { get; private init; }
    public string? Domain { get; private init; }
    public string? PublishedUrl { get; private init; }
    public string? TemplateId { get; private init; }

    public static TenantWebsitePackageResult Ok(
        byte[] zipFile,
        string script,
        string instructions,
        string fileName,
        string domain,
        string publishedUrl,
        string templateId) =>
        new()
        {
            Succeeded = true,
            ZipFile = zipFile,
            Script = script,
            Instructions = instructions,
            FileName = fileName,
            Domain = domain,
            PublishedUrl = publishedUrl,
            TemplateId = templateId
        };

    public static TenantWebsitePackageResult Fail(string code, string error) =>
        new()
        {
            Succeeded = false,
            Code = code,
            Error = error
        };
}

public interface ITenantWebsiteGenerator
{
    /// <summary>
    /// Builds a static website ZIP for <paramref name="domain"/> from tenant company/catalog data.
    /// Also publishes a platform copy under the configured media sites root.
    /// </summary>
    Task<TenantWebsitePackageResult> GenerateWebsiteAsync(
        Guid tenantId,
        string domain,
        string? templateId = null,
        CancellationToken ct = default);
}
