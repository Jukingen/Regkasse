using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Configuration;

/// <summary>
/// Loads PEM material from <see cref="LicenseSettingsOptions"/> paths when the main
/// <see cref="LicenseOptions"/> PEM fields are not set inline.
/// </summary>
public sealed class LicenseOptionsFromFilesPostConfigure : IPostConfigureOptions<LicenseOptions>
{
    private readonly IOptions<LicenseSettingsOptions> _fileSettings;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LicenseOptionsFromFilesPostConfigure> _logger;

    public LicenseOptionsFromFilesPostConfigure(
        IOptions<LicenseSettingsOptions> fileSettings,
        IWebHostEnvironment env,
        ILogger<LicenseOptionsFromFilesPostConfigure> logger)
    {
        _fileSettings = fileSettings;
        _env = env;
        _logger = logger;
    }

    public void PostConfigure(string? name, LicenseOptions options)
    {
        var fs = _fileSettings.Value;

        if (!string.IsNullOrWhiteSpace(fs.Issuer))
            options.LicenseJwtIssuer = fs.Issuer.Trim();

        if (!string.IsNullOrWhiteSpace(fs.Audience))
            options.LicenseJwtAudience = fs.Audience.Trim();

        TryLoadPemFromPath(
            options,
            static o => o.OfflineVerificationPublicKeyPem,
            static (o, v) => o.OfflineVerificationPublicKeyPem = v,
            fs.PublicKeyPath,
            "LicenseSettings:PublicKeyPath → License:OfflineVerificationPublicKeyPem");

        TryLoadPemFromPath(
            options,
            static o => o.SigningPrivateKeyPem,
            static (o, v) => o.SigningPrivateKeyPem = v,
            fs.PrivateKeyPath,
            "LicenseSettings:PrivateKeyPath → License:SigningPrivateKeyPem");

        LicenseGracePeriodConfig.ApplyFrom(options);
    }

    private void TryLoadPemFromPath(
        LicenseOptions options,
        Func<LicenseOptions, string?> getPem,
        Action<LicenseOptions, string> setPem,
        string? path,
        string logLabel)
    {
        if (!string.IsNullOrWhiteSpace(getPem(options)?.Trim()))
            return;

        if (string.IsNullOrWhiteSpace(path))
            return;

        var resolved = Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(_env.ContentRootPath, path));

        if (!File.Exists(resolved))
        {
            _logger.LogWarning("License PEM path not found for {Label}: {Path}", logLabel, resolved);
            return;
        }

        try
        {
            setPem(options, File.ReadAllText(resolved));
            _logger.LogInformation("Loaded license PEM for {Label} from {Path}.", logLabel, resolved);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed reading license PEM for {Label} from {Path}.", logLabel, resolved);
        }
    }
}
