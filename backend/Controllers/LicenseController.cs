using System.Globalization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

/// <summary>Anonymous-friendly license status for POS (no secrets).</summary>
[ApiController]
[Route("api/license")]
[Produces("application/json")]
public sealed class LicenseController : ControllerBase
{
    private readonly ILicenseService _licenseService;
    private readonly IOptions<LicenseOptions> _licenseOptions;
    private readonly IWebHostEnvironment _environment;

    public LicenseController(
        ILicenseService licenseService,
        IOptions<LicenseOptions> licenseOptions,
        IWebHostEnvironment environment)
    {
        _licenseService = licenseService;
        _licenseOptions = licenseOptions;
        _environment = environment;
    }

    /// <summary>Current license snapshot (trial vs paid, expiry, coarse feature tags).</summary>
    [HttpGet("status")]
    [AllowAnonymous]
    public ActionResult<LicensePublicStatusDto> GetPublicStatus()
    {
        // Development-only: POS reads this endpoint for banners; bypass stored license so local work is not blocked.
        if (_environment.IsDevelopment())
        {
            var days = LicenseService.TrialDays;
            var untilUtc = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(days), DateTimeKind.Utc);
            return Ok(new LicensePublicStatusDto
            {
                LicenseType = "Trial",
                ValidUntil = untilUtc.ToString("o", CultureInfo.InvariantCulture),
                DaysRemaining = days,
                Features = new[] { "pos", "admin", "fiscal" },
                IsExpired = false,
                IsValid = true,
            });
        }

        var s = _licenseService.GetStatus();
        return Ok(MapPublicStatus(s));
    }

    /// <summary>Optional POS-facing feature limits (configuration-driven).</summary>
    [HttpGet("features")]
    [AllowAnonymous]
    public ActionResult<LicenseFeaturesDto> GetFeatures()
    {
        var o = _licenseOptions.Value;
        return Ok(new LicenseFeaturesDto
        {
            AllowOffline = o.LicenseFeatureAllowOffline,
            MaxCashiers = o.LicenseFeatureMaxCashiers,
        });
    }

    private static LicensePublicStatusDto MapPublicStatus(LicenseStatusResponse s)
    {
        var paid = s.IsValid && !s.IsTrial;
        var trialActive = s.IsTrial && !s.IsExpired;
        var licenseType = paid ? "Paid" : "Trial";
        var isValidPublic = paid || trialActive;

        IReadOnlyList<string> features;
        if (s.IsExpired && !trialActive && !paid)
            features = Array.Empty<string>();
        else
            features = new[] { "pos", "admin", "fiscal" };

        var validUntil = s.ExpiryDate.HasValue
            ? DateTime.SpecifyKind(s.ExpiryDate.Value, DateTimeKind.Utc).ToString("o")
            : null;

        return new LicensePublicStatusDto
        {
            LicenseType = licenseType,
            ValidUntil = validUntil,
            DaysRemaining = s.DaysRemaining,
            Features = features,
            IsExpired = s.IsExpired,
            IsValid = isValidPublic,
        };
    }
}
