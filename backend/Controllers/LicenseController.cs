using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Anonymous-friendly license endpoints for POS bootstrap (status, features, activation) — no JWT required.
/// POS may call these before any cashier signs in.
/// </summary>
[ApiController]
[Route("api/license")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class LicenseController : ControllerBase
{
    /// <summary>Optional client-reported SHA-256 machine hash (hex); must match this host when set. Same as JSON <c>machineFingerprint</c>.</summary>
    public const string MachineFingerprintHttpHeader = "X-Machine-Fingerprint";

    private readonly ILicenseService _licenseService;
    private readonly IOptions<LicenseOptions> _licenseOptions;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<LicenseController> _logger;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly AppDbContext _db;

    public LicenseController(
        ILicenseService licenseService,
        IOptions<LicenseOptions> licenseOptions,
        IWebHostEnvironment environment,
        ILogger<LicenseController> logger,
        ICurrentTenantAccessor tenantAccessor,
        AppDbContext db)
    {
        _licenseService = licenseService;
        _licenseOptions = licenseOptions;
        _environment = environment;
        _logger = logger;
        _tenantAccessor = tenantAccessor;
        _db = db;
    }

    /// <summary>Current license snapshot (trial vs paid, expiry, coarse feature tags, display mode).</summary>
    [HttpGet("status")]
    public async Task<ActionResult<LicensePublicStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        // Same mapping in all environments so POS/admin anonymous status matches activated license
        // Optional synthetic licensing is controlled by <see cref="IDevelopmentModeService.ShouldBypassLicense"/> (Development host only).
        var s = await _licenseService.GetCurrentStatusAsync(cancellationToken).ConfigureAwait(false);
        return Ok(MapPublicStatus(s));
    }

    /// <summary>Optional POS-facing feature limits (configuration-driven).</summary>
    [HttpGet("features")]
    public async Task<ActionResult<LicenseFeaturesDto>> GetFeatures(CancellationToken cancellationToken)
    {
        var lic = await _licenseService.GetCurrentStatusAsync(cancellationToken).ConfigureAwait(false);
        var licenseFeatures = lic.EnabledFeatures is { Count: > 0 } ? lic.EnabledFeatures : LicenseFeatureIds.All;

        if (_environment.IsDevelopment())
        {
            return Ok(new LicenseFeaturesDto
            {
                AllowOffline = true,
                MaxCashiers = -1,
                EnabledLicenseFeatures = licenseFeatures,
            });
        }

        var o = _licenseOptions.Value;
        return Ok(new LicenseFeaturesDto
        {
            AllowOffline = o.LicenseFeatureAllowOffline,
            MaxCashiers = o.LicenseFeatureMaxCashiers,
            EnabledLicenseFeatures = licenseFeatures,
        });
    }

    /// <summary>
    /// <b>Single activation endpoint</b> for POS and frontend-admin: REGK display key (and optional offline JWT in the body).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>No authentication required</b> — POS may activate before any cashier logs in. FA usually calls while authenticated.</description></item>
    /// <item><description>When Bearer JWT is present, <c>app_context</c> claim and user id are used for audit (<c>created_by_user_id</c>).</description></item>
    /// <item><description>When anonymous, send <c>X-App-Context: pos</c> or <c>X-App-Context: admin</c> (see <see cref="ClientAppPolicy.AppContextHttpHeader"/>) so the activation attempt records the client app.</description></item>
    /// <item><description>Machine binding uses this deployment&apos;s canonical fingerprint from <see cref="ILicenseStorageService"/> (not IP/User-Agent heuristics). Optional JSON <c>machineFingerprint</c> or header <see cref="MachineFingerprintHttpHeader"/> must match the server hash when set.</description></item>
    /// </list>
    /// </remarks>
    [HttpPost("activate")]
    public async Task<ActionResult<LicenseActivationResult>> ActivateLicense(
        [FromBody] ActivateLicenseRequest? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
        {
            _logger.LogWarning("License activation: request body bound to null.");
            return BadRequest(new LicenseActivationResult(false, "Request body is required."));
        }

        ApplyOptionalMachineFingerprintFromHeader(body);

        var appContext = ResolveActivationSourceAppContext(HttpContext);
        var initiatorUserId = ResolveInitiatingUserId(HttpContext);

        _logger.LogInformation(
            "License activation request. AppContext={AppContext}, Authenticated={Authenticated}, LicenseKeyPresent={LicenseKeyPresent}, LicenseKeyPrefix={LicenseKeyPrefix}, OfflineJwtPresent={OfflineJwtPresent}, OfflineJwtLength={OfflineJwtLength}",
            appContext ?? "(none)",
            initiatorUserId.HasValue,
            !string.IsNullOrWhiteSpace(body.LicenseKey),
            SafeFieldPrefix(body.LicenseKey),
            !string.IsNullOrWhiteSpace(body.OfflineActivationJwt),
            body.OfflineActivationJwt?.Length ?? 0);

        var uaRaw = Request.Headers.UserAgent.ToString();
        var ua = uaRaw.Length > 500 ? uaRaw[..500] : uaRaw;
        var client = new LicenseActivationClientInfo(
            ResolveClientIp(HttpContext),
            string.IsNullOrEmpty(ua) ? null : ua,
            initiatorUserId,
            appContext);
        var result = await _licenseService.ActivateAsync(body, client, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            _logger.LogWarning("License activation failed: {Message}", result.Message);
            return BadRequest(result);
        }

        var enriched = await EnrichActivationResultWithTenantAsync(result, cancellationToken).ConfigureAwait(false);
        return Ok(enriched);
    }

    private async Task<LicenseActivationResult> EnrichActivationResultWithTenantAsync(
        LicenseActivationResult result,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId == Guid.Empty)
            tenantId = LegacyDefaultTenantIds.Primary;

        var tenant = await _db.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Id, t.Slug })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var resolvedId = tenant?.Id ?? tenantId;
        var slug = tenant?.Slug;
        if (string.IsNullOrWhiteSpace(slug))
            slug = LegacyDefaultTenantIds.PrimarySlug;

        return result with
        {
            TenantId = resolvedId,
            TenantSlug = slug,
            ApiBaseUrl = BuildApiBaseUrl(Request),
        };
    }

    private static string BuildApiBaseUrl(HttpRequest request)
    {
        var pathBase = request.PathBase.Value?.TrimEnd('/') ?? string.Empty;
        return $"{request.Scheme}://{request.Host}{pathBase}/api";
    }

    /// <summary>Copies <see cref="MachineFingerprintHttpHeader"/> into the request DTO when the JSON body omitted <c>machineFingerprint</c>.</summary>
    private void ApplyOptionalMachineFingerprintFromHeader(ActivateLicenseRequest body)
    {
        if (!string.IsNullOrWhiteSpace(body.MachineFingerprint))
            return;

        if (!Request.Headers.TryGetValue(MachineFingerprintHttpHeader, out var values) || values.Count == 0)
            return;

        var raw = values.ToString().Trim();
        if (raw.Length == 0)
            return;

        body.MachineFingerprint = raw;
        _logger.LogInformation("License activation: machineFingerprint taken from {Header} (length={Length}).", MachineFingerprintHttpHeader, raw.Length);
    }

    private static string? ResolveActivationSourceAppContext(HttpContext httpContext)
    {
        var fromClaim = httpContext.User?.FindFirst(ClientAppPolicy.AppContextClaimType)?.Value;
        if (!string.IsNullOrWhiteSpace(fromClaim))
            return fromClaim.Trim().ToLowerInvariant();

        if (httpContext.Request.Headers.TryGetValue(ClientAppPolicy.AppContextHttpHeader, out var header)
            && header.Count > 0)
        {
            var v = header.ToString().Trim();
            if (ClientAppPolicy.IsKnownApp(v))
                return v.ToLowerInvariant();
        }

        return null;
    }

    private static Guid? ResolveInitiatingUserId(HttpContext httpContext)
    {
        if (httpContext.User?.Identity?.IsAuthenticated != true)
            return null;
        return Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }

    private static string? ResolveClientIp(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) && forwarded.Count > 0)
        {
            var first = forwarded.ToString().Split(',')[0].Trim();
            if (first.Length == 0)
                return null;
            return first.Length <= 45 ? first : first[..45];
        }

        var remote = httpContext.Connection.RemoteIpAddress;
        if (remote is null)
            return null;
        if (remote.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            && remote.IsIPv4MappedToIPv6)
            return remote.MapToIPv4().ToString();
        return remote.ToString();
    }

    private static string SafeFieldPrefix(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "(empty)";
        var trimmed = value.Trim();
        return trimmed.Length <= 12 ? trimmed : trimmed[..12] + "…";
    }

    private static LicensePublicStatusDto MapPublicStatus(LicenseStatusResponse s)
    {
        var paid = s.IsValid && !s.IsTrial;
        var trialActive = s.IsTrial && !s.IsExpired;
        var licenseType = paid ? "Licensed" : trialActive ? "Trial" : "Expired";
        var isValidPublic = paid || trialActive;

        IReadOnlyList<string> features;
        if (!isValidPublic)
            features = Array.Empty<string>();
        else
            features = s.EnabledFeatures is { Count: > 0 } ? s.EnabledFeatures : LicenseFeatureIds.All;

        DateTime? validUntil = s.ExpiryDate.HasValue
            ? DateTime.SpecifyKind(s.ExpiryDate.Value, DateTimeKind.Utc)
            : null;

        var mode = trialActive ? "Trial" : "Production";

        return new LicensePublicStatusDto
        {
            LicenseType = licenseType,
            ValidUntil = validUntil,
            DaysRemaining = s.DaysRemaining,
            Features = features,
            IsExpired = s.IsExpired,
            IsValid = isValidPublic,
            Mode = mode,
            IsDevelopmentBypass = s.IsDevelopmentBypass,
        };
    }
}
