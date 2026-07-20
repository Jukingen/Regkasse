using System.IO.Compression;
using System.Text;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.App;

/// <summary>
/// Downloadable tenant app package (ZIP + store publish instructions).
/// Reuses <see cref="AppGeneratorService"/> — does not invent a parallel generator.
/// Native: Expo/RN source ZIP (no on-host Xcode/Gradle compile). PWA: installable static files as ZIP.
/// </summary>
public sealed class TenantAppGenerator : ITenantAppGenerator
{
    public const string DisabledCode = AppGeneratorService.DisabledCode;
    public const string TenantNotFoundCode = AppGeneratorService.TenantNotFoundCode;
    public const string InvalidSlugCode = AppGeneratorService.InvalidSlugCode;
    public const string UnsupportedAppTypeCode = AppGeneratorService.UnsupportedAppTypeCode;
    public const string GenerateFailedCode = "APP_PACKAGE_FAILED";

    private readonly AppGeneratorService _app;
    private readonly IOptions<WebsiteGeneratorOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<TenantAppGenerator> _logger;

    public TenantAppGenerator(
        AppGeneratorService app,
        IOptions<WebsiteGeneratorOptions> options,
        IHostEnvironment environment,
        ILogger<TenantAppGenerator> logger)
    {
        _app = app;
        _options = options;
        _environment = environment;
        _logger = logger;
    }

    public async Task<TenantAppPackageResult> GenerateAppAsync(
        Guid tenantId,
        AppType type,
        CancellationToken ct = default)
    {
        if (type is not (AppType.Pwa or AppType.Native))
            return TenantAppPackageResult.Fail(UnsupportedAppTypeCode, "Unsupported app type.");

        var published = await _app.GenerateAppAsync(tenantId, type, ct);
        if (!published.Succeeded)
        {
            return TenantAppPackageResult.Fail(
                published.Code ?? GenerateFailedCode,
                published.Error ?? "App generation failed.");
        }

        var tenant = await _app.GetTenantDataAsync(tenantId, ct);
        if (tenant is null)
            return TenantAppPackageResult.Fail(TenantNotFoundCode, "Tenant not found.");

        try
        {
            var instructions = BuildInstructions(type, tenant.Slug, tenant.DisplayName);
            var root = ResolveRoot();
            byte[] zipFile;
            string fileName;

            if (type == AppType.Native)
            {
                var sourcePath = Path.Combine(root, tenant.Slug, "app-native", "app-source.zip");
                if (!File.Exists(sourcePath))
                    return TenantAppPackageResult.Fail(GenerateFailedCode, "Native app package was not written.");

                var sourceZip = await File.ReadAllBytesAsync(sourcePath, ct);
                zipFile = CreateZip(new GeneratedAppFile[]
                {
                    new("app-source.zip", sourceZip),
                    new("INSTRUCTIONS.txt", Encoding.UTF8.GetBytes(instructions))
                });
                fileName = $"{tenant.Slug}-native-app.zip";
            }
            else
            {
                var pwaDir = Path.Combine(root, tenant.Slug, "app");
                if (!Directory.Exists(pwaDir))
                    return TenantAppPackageResult.Fail(GenerateFailedCode, "PWA app files were not written.");

                var files = new List<GeneratedAppFile>();
                foreach (var path in Directory.EnumerateFiles(pwaDir, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(pwaDir, path).Replace('\\', '/');
                    files.Add(new GeneratedAppFile(relative, await File.ReadAllBytesAsync(path, ct)));
                }

                files.Add(new GeneratedAppFile("INSTRUCTIONS.txt", Encoding.UTF8.GetBytes(instructions)));
                zipFile = CreateZip(files);
                fileName = $"{tenant.Slug}-pwa-app.zip";
            }

            return TenantAppPackageResult.Ok(
                zipFile: zipFile,
                instructions: instructions,
                fileName: fileName,
                downloadUrl: published.DownloadUrl ?? string.Empty,
                appType: type);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Tenant app package failed for tenant {TenantId} type {AppType}",
                tenantId,
                type);
            return TenantAppPackageResult.Fail(GenerateFailedCode, "App package generation failed.");
        }
    }

    private string ResolveRoot()
    {
        var opts = _options.Value;
        return string.IsNullOrWhiteSpace(opts.RootRelativeDirectory)
            ? _environment.ContentRootPath
            : Path.Combine(_environment.ContentRootPath, opts.RootRelativeDirectory);
    }

    private static byte[] CreateZip(IReadOnlyList<GeneratedAppFile> files)
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

    private static string BuildInstructions(AppType type, string slug, string displayName)
    {
        if (type == AppType.Pwa)
        {
            return $"""
                Regkasse PWA package — {displayName} ({slug})
                =============================================

                1. Extract this ZIP to your static web host (or use the platform copy under /media/sites/{slug}/app/).
                2. Serve over HTTPS so install prompts work.
                3. Open the site on a phone and use "Add to Home Screen" / Install.

                Notes:
                - The API host does not produce APK/IPA for PWA mode.
                - Live menu API path is embedded in config.json (public catalog).
                """;
        }

        return $"""
            Regkasse native (Expo) package — {displayName} ({slug})
            ========================================================

            The archive contains app-source.zip (Expo / React Native sources).
            The Regkasse API host does NOT run Xcode, Android Studio, or EAS builds.

            iOS:
            1. Extract app-source.zip
            2. Install dependencies (npm install / yarn)
            3. Open the project with Expo / Xcode as documented in the package README
            4. Configure signing (Apple Developer)
            5. Archive and publish to the App Store (or TestFlight)

            Android:
            1. Extract app-source.zip
            2. Install dependencies (npm install / yarn)
            3. Open with Expo / Android Studio
            4. Build a signed APK/AAB
            5. Publish to Google Play

            Platform preview (source ZIP on API host):
            /media/sites/{slug}/app-native/app-source.zip
            """;
    }
}

public sealed record GeneratedAppFile(string Name, byte[] Content);

/// <summary>Outcome of <see cref="ITenantAppGenerator.GenerateAppAsync"/>.</summary>
public sealed class TenantAppPackageResult
{
    public bool Succeeded { get; private init; }
    public string? Code { get; private init; }
    public string? Error { get; private init; }
    public byte[]? ZipFile { get; private init; }
    public string? Instructions { get; private init; }
    public string? FileName { get; private init; }
    public string? DownloadUrl { get; private init; }
    public AppType? AppType { get; private init; }

    public static TenantAppPackageResult Ok(
        byte[] zipFile,
        string instructions,
        string fileName,
        string downloadUrl,
        AppType appType) =>
        new()
        {
            Succeeded = true,
            ZipFile = zipFile,
            Instructions = instructions,
            FileName = fileName,
            DownloadUrl = downloadUrl,
            AppType = appType
        };

    public static TenantAppPackageResult Fail(string code, string error) =>
        new()
        {
            Succeeded = false,
            Code = code,
            Error = error
        };
}

public interface ITenantAppGenerator
{
    /// <summary>
    /// Builds a downloadable app ZIP for the tenant and publishes a platform copy via
    /// <see cref="AppGeneratorService"/>.
    /// </summary>
    Task<TenantAppPackageResult> GenerateAppAsync(
        Guid tenantId,
        AppType type,
        CancellationToken ct = default);
}
