using System;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// POS APK self-update probe. Anonymous so the app can check before login and even after its
/// stored credentials have expired — the response carries no PII and no auth-protected fields.
/// Source of truth is <see cref="AppUpdateOptions"/> (config-only; no DB access).
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/app")]
[Produces("application/json")]
public sealed class AppVersionController : ControllerBase
{
    private readonly IOptionsMonitor<AppUpdateOptions> _options;

    public AppVersionController(IOptionsMonitor<AppUpdateOptions> options)
    {
        _options = options;
    }

    /// <summary>
    /// Returns the latest published POS APK metadata (versionCode, downloadUrl, mandatory flag, …).
    /// Cached for 60 seconds at the edge; clients also throttle their own checks.
    /// </summary>
    [HttpGet("version")]
    [ProducesResponseType(typeof(AppVersionResponseDto), StatusCodes.Status200OK)]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, NoStore = false)]
    public ActionResult<AppVersionResponseDto> GetLatestVersion()
    {
        var o = _options.CurrentValue;
        var dto = new AppVersionResponseDto
        {
            LatestVersionCode = o.LatestVersionCode,
            LatestVersionName = string.IsNullOrWhiteSpace(o.LatestVersionName) ? string.Empty : o.LatestVersionName,
            DownloadUrl = string.IsNullOrWhiteSpace(o.DownloadUrl) ? null : o.DownloadUrl,
            ReleaseNotesUrl = string.IsNullOrWhiteSpace(o.ReleaseNotesUrl) ? null : o.ReleaseNotesUrl,
            Mandatory = o.Mandatory,
            MinimumSupportedVersionCode = o.MinimumSupportedVersionCode,
            Sha256 = string.IsNullOrWhiteSpace(o.Sha256) ? null : o.Sha256!.ToLowerInvariant(),
            SizeBytes = o.SizeBytes,
            ServerTimeUtc = DateTime.UtcNow,
        };
        return Ok(dto);
    }
}
