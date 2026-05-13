using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using KasseAPI_Final;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KasseAPI_Final.Services;

/// <summary>License evaluation for on-premise deployments (trial, offline JWS, optional remote check).
/// Persistence is delegated to <see cref="ILicenseStorageService"/> (cross-platform encrypted file).</summary>
public interface ILicenseService
{
    /// <summary>Load state, evaluate trial/license, log warnings. Call once per process startup.</summary>
    void EvaluateOnStartup();

    LicenseStatusResponse GetStatus();

    /// <summary>True only after <see cref="EvaluateOnStartup"/> has produced a real snapshot (used by header/visibility middleware to distinguish "None" from "Expired").</summary>
    bool IsLicenseSnapshotInitialized { get; }

    /// <summary>Lightweight async validation used by <see cref="Middleware.LicenseMiddleware"/> (snapshot + revocation overlay).</summary>
    Task<LicenseValidationResult> ValidateAsync(CancellationToken cancellationToken = default);

    Task<LicenseActivationResult> ActivateAsync(ActivateLicenseRequest request, CancellationToken cancellationToken = default);
}

/// <summary>In-app license reminder surfaced via <c>/api/health/license</c> and admin license status (populated by store, not <see cref="LicenseService"/>).</summary>
public sealed record LicenseReminderNotice(
    string Code,
    string Severity,
    string Message,
    DateTimeOffset RaisedAtUtc);

public sealed record LicenseStatusResponse(
    bool IsValid,
    bool IsTrial,
    bool IsExpired,
    int DaysRemaining,
    DateTime? ExpiryDate,
    string MachineHash,
    IReadOnlyList<LicenseReminderNotice>? Reminders = null);

public sealed class ActivateLicenseRequest
{
    [Required]
    public string LicenseKey { get; set; } = "";

    public string? OfflineActivationJwt { get; set; }

    /// <summary>Optional client-reported fingerprint; when set, must match this host.</summary>
    [JsonPropertyName("machineFingerprint")]
    public string? MachineFingerprint { get; set; }
}

public sealed record LicenseActivationResult(bool Success, string? Message, DateTime? ValidUntil = null);

/// <summary>
/// Lisans iş kurallarını yürütür; saklama (şifreli dosya) tamamen <see cref="ILicenseStorageService"/>'e delege edilir.
/// Registry/DPAPI/Windows'a özgü kod yoktur — Windows, Linux ve macOS üzerinde aynı şekilde çalışır.
/// </summary>
public sealed class LicenseService : ILicenseService
{
    public const int TrialDays = 30;

    private static readonly JsonSerializerOptions RemoteSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Regex LicenseKeyRegex = new(
        @"^REGK-[A-Z0-9]{5}-[A-Z0-9]{5}-[A-Z0-9]{5}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IOptions<LicenseOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILicenseStorageService _storage;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<LicenseService> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IOptionsMonitor<DevelopmentOptions> _developmentOptions;

    private readonly object _gate = new();
    private LicenseStatusResponse _snapshot = new(false, false, false, 0, null, "");
    private LicensePersistedState? _persisted;
    private bool _snapshotInitialized;

    public LicenseService(
        IOptions<LicenseOptions> options,
        IHttpClientFactory httpClientFactory,
        ILicenseStorageService storage,
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<LicenseService> logger,
        IHostEnvironment hostEnvironment,
        IOptionsMonitor<DevelopmentOptions> developmentOptions)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _storage = storage;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
        _developmentOptions = developmentOptions;
    }

    public bool IsLicenseSnapshotInitialized
    {
        get
        {
            lock (_gate)
                return _snapshotInitialized;
        }
    }

    public void EvaluateOnStartup()
    {
        if (OpenApiExportMode.IsEnabled)
        {
            lock (_gate)
            {
                _snapshot = new LicenseStatusResponse(true, false, false, 0, null, "openapi-export");
                _snapshotInitialized = true;
            }
            return;
        }

        lock (_gate)
        {
            _persisted = LoadOrCreatePersisted();
            var paid = TryValidatePaidLicense(_persisted);
            _snapshot = BuildSnapshot(_persisted, paid);
            _snapshotInitialized = true;

            if (paid)
                _logger.LogInformation(
                    "License: valid paid license for machine hash prefix {Prefix}.",
                    SafePrefix(_storage.MachineHashHex));
            else if (_snapshot.IsTrial)
                _logger.LogWarning(
                    "License: trial mode active ({Days} day(s) remaining).",
                    _snapshot.DaysRemaining);
            else
                _logger.LogWarning(
                    "License: trial expired and no valid license — payment creation will be blocked. Machine hash prefix {Prefix}.",
                    SafePrefix(_storage.MachineHashHex));
        }
    }

    public LicenseStatusResponse GetStatus()
    {
        string? persistedKeyForRevocation = null;
        LicenseStatusResponse snapshot;

        lock (_gate)
        {
            // EvaluateOnStartup henüz çağrılmadıysa snapshot'ın MachineHash alanı boş olur; en azından makine kimliğini doldurarak dönelim.
            if (string.IsNullOrEmpty(_snapshot.MachineHash))
                _snapshot = _snapshot with { MachineHash = _storage.MachineHashHex };

            snapshot = _snapshot;
            if (!OpenApiExportMode.IsEnabled
                && _persisted != null
                && !string.IsNullOrWhiteSpace(_persisted.LicenseKey))
            {
                persistedKeyForRevocation = _persisted.LicenseKey.Trim().ToUpperInvariant();
            }
        }

        if (persistedKeyForRevocation is not null && IsPersistedLicenseKeyBlockedInRegistry(persistedKeyForRevocation))
        {
            snapshot = snapshot with
            {
                IsValid = false,
                IsTrial = false,
                IsExpired = true,
                DaysRemaining = 0,
            };
        }

        if (!OpenApiExportMode.IsEnabled
            && _hostEnvironment.IsDevelopment()
            && _developmentOptions.CurrentValue.SimulateLicenseExpired)
        {
            return snapshot with
            {
                IsValid = false,
                IsTrial = false,
                IsExpired = true,
                DaysRemaining = 0,
                ExpiryDate = DateTime.UtcNow.Date,
            };
        }

        return snapshot;
    }

    public Task<LicenseValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var s = GetStatus();
        var paid = s.IsValid && !s.IsTrial;
        var trialActive = s.IsTrial && !s.IsExpired;
        var operational = paid || trialActive;
        return Task.FromResult(new LicenseValidationResult
        {
            IsLicenseOperational = operational,
            IsTrial = s.IsTrial,
            IsExpired = s.IsExpired,
            IsPaidValid = paid,
            DaysRemaining = s.DaysRemaining,
            ExpiryUtc = s.ExpiryDate,
        });
    }

    public async Task<LicenseActivationResult> ActivateAsync(ActivateLicenseRequest request, CancellationToken cancellationToken = default)
    {
        if (OpenApiExportMode.IsEnabled)
            return new LicenseActivationResult(false, "License activation is disabled in OpenAPI export mode.");

        if (request is null || string.IsNullOrWhiteSpace(request.LicenseKey))
            return new LicenseActivationResult(false, "LicenseKey is required.");

        var normalizedKey = request.LicenseKey.Trim().ToUpperInvariant();
        if (!LicenseKeyRegex.IsMatch(normalizedKey))
            return new LicenseActivationResult(false, "Invalid license key format. Expected REGK-XXXXX-XXXXX-XXXXX.");

        if (!string.IsNullOrWhiteSpace(request.MachineFingerprint))
        {
            var fp = request.MachineFingerprint.Trim();
            if (!string.Equals(fp, _storage.MachineHashHex, StringComparison.OrdinalIgnoreCase))
            {
                return new LicenseActivationResult(false, "Machine fingerprint does not match this host.");
            }
        }

        lock (_gate)
        {
            _persisted ??= LoadOrCreatePersisted();
        }

        var opts = _options.Value;
        var offlineJwt = request.OfflineActivationJwt?.Trim();

        try
        {
            if (!string.IsNullOrEmpty(offlineJwt))
            {
                if (string.IsNullOrWhiteSpace(opts.OfflineVerificationPublicKeyPem))
                    return new LicenseActivationResult(false, "OfflineVerificationPublicKeyPem is not configured.");

                if (!TryVerifyOfflineJwt(offlineJwt, normalizedKey, out var err))
                    return new LicenseActivationResult(false, err ?? "Offline JWT verification failed.");

                if (IsPersistedLicenseKeyBlockedInRegistry(normalizedKey))
                {
                    return new LicenseActivationResult(
                        false,
                        "This license key was transferred or revoked. Activate using the replacement key provided by support.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(opts.RemoteValidationUrl))
            {
                var remoteOk = await ValidateRemoteAsync(normalizedKey, cancellationToken).ConfigureAwait(false);
                if (!remoteOk)
                    return new LicenseActivationResult(false, "Remote license server did not accept this key for this machine.");

                if (IsPersistedLicenseKeyBlockedInRegistry(normalizedKey))
                {
                    return new LicenseActivationResult(
                        false,
                        "This license key was transferred or revoked. Activate using the replacement key provided by support.");
                }
            }
            else
            {
                return new LicenseActivationResult(
                    false,
                    "Provide OfflineActivationJwt (offline) or configure License:RemoteValidationUrl (online).");
            }

            lock (_gate)
            {
                _persisted ??= new LicensePersistedState { FirstRunUtc = DateTime.UtcNow };
                _persisted.LicenseKey = normalizedKey;
                _persisted.OfflineJwt = offlineJwt;
                _storage.SaveLicenseToFile(_persisted);
                var paid = TryValidatePaidLicense(_persisted);
                _snapshot = BuildSnapshot(_persisted, paid);
                _snapshotInitialized = true;
            }

            _logger.LogInformation("License: activation succeeded for key prefix {Prefix}.", SafePrefix(normalizedKey));
            var st = GetStatus();
            return new LicenseActivationResult(true, "License activated.", st.ExpiryDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "License: activation failed with unexpected error.");
            return new LicenseActivationResult(false, "Activation failed due to an internal error.");
        }
    }

    private LicenseStatusResponse BuildSnapshot(LicensePersistedState blob, bool paidValid)
    {
        // Snapshot tek bir noktada hesaplanır: paid → JWT exp; trial → FirstRunUtc + TrialDays;
        // expired → en son geçerli expiry tarihi (UI bilgilendirmesi için tutulur).
        var trialEndUtc = blob.FirstRunUtc.AddDays(TrialDays);
        var jwtExpUtc = string.IsNullOrWhiteSpace(blob.OfflineJwt)
            ? null
            : TryGetJwtExp(blob.OfflineJwt!.Trim());

        var nowUtc = DateTime.UtcNow;

        if (paidValid)
        {
            // Paid lisans aktif: ExpiryDate = JWT exp varsa o, yoksa null (süresiz/exp claim'i yok).
            // DaysRemaining: exp varsa kalan gün; yoksa "uzak" anlamına gelen sentinel (365) → ≤15 banner tetiklenmez.
            var expiryUtc = jwtExpUtc;
            var days = expiryUtc.HasValue
                ? Math.Max(0, (int)Math.Ceiling((expiryUtc.Value - nowUtc).TotalDays))
                : 365;
            return new LicenseStatusResponse(
                IsValid: true,
                IsTrial: false,
                IsExpired: false,
                DaysRemaining: days,
                ExpiryDate: expiryUtc,
                MachineHash: _storage.MachineHashHex);
        }

        if (nowUtc < trialEndUtc)
        {
            // Trial aktif.
            var remaining = (int)Math.Ceiling((trialEndUtc - nowUtc).TotalDays);
            remaining = Math.Clamp(remaining, 0, TrialDays);
            return new LicenseStatusResponse(
                IsValid: false,
                IsTrial: true,
                IsExpired: false,
                DaysRemaining: remaining,
                ExpiryDate: trialEndUtc,
                MachineHash: _storage.MachineHashHex);
        }

        // Hem trial bitmiş hem geçerli ücretli lisans yok → expired.
        // ExpiryDate'i en son geçerli expiry olarak ver (paid varsa JWT exp, yoksa trial sonu).
        return new LicenseStatusResponse(
            IsValid: false,
            IsTrial: false,
            IsExpired: true,
            DaysRemaining: 0,
            ExpiryDate: jwtExpUtc ?? trialEndUtc,
            MachineHash: _storage.MachineHashHex);
    }

    /// <summary>
    /// Best-effort JWT <c>exp</c> claim okur (imza doğrulamadan); yalnızca UI bilgilendirmesi için.
    /// İmza ve geçerlilik kontrolü <see cref="TryVerifyOfflineJwt"/> içinde ayrıca yapılır.
    /// </summary>
    private static DateTime? TryGetJwtExp(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3)
                return null;

            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("exp", out var expEl)
                && expEl.ValueKind == JsonValueKind.Number
                && expEl.TryGetInt64(out var expUnix))
            {
                return DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
            }
        }
        catch
        {
            // best-effort: expiry tarihi gösterilemese bile validation çalışmaya devam eder.
        }

        return null;
    }

    private bool TryValidatePaidLicense(LicensePersistedState blob)
    {
        if (string.IsNullOrWhiteSpace(blob.LicenseKey))
            return false;

        if (!LicenseKeyRegex.IsMatch(blob.LicenseKey.Trim()))
            return false;

        var key = blob.LicenseKey.Trim().ToUpperInvariant();

        if (IsPersistedLicenseKeyBlockedInRegistry(key))
            return false;

        var opts = _options.Value;

        if (!string.IsNullOrWhiteSpace(blob.OfflineJwt) && !string.IsNullOrWhiteSpace(opts.OfflineVerificationPublicKeyPem))
            return TryVerifyOfflineJwt(blob.OfflineJwt.Trim(), key, out _);

        if (!string.IsNullOrWhiteSpace(opts.RemoteValidationUrl))
        {
            // Senkron sentinel kontrol; başlangıçta blocking kullanmamak için cache benzeri optimist yol.
            try
            {
                return ValidateRemoteAsync(key, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "License: remote re-validation failed; treating as not valid for paid mode.");
                return false;
            }
        }

        return false;
    }

    private async Task<bool> ValidateRemoteAsync(string licenseKey, CancellationToken cancellationToken)
    {
        var url = _options.Value.RemoteValidationUrl?.Trim();
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var client = _httpClientFactory.CreateClient("LicenseRemote");
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new RemoteLicenseRequest(_storage.MachineHashHex, licenseKey)),
        };
        var apiKey = _options.Value.RemoteValidationApiKey;
        if (!string.IsNullOrWhiteSpace(apiKey))
            req.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);

        using var resp = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return false;

        var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
            return true;

        try
        {
            var dto = JsonSerializer.Deserialize<RemoteLicenseResponseDto>(json, RemoteSerializerOptions);
            return dto?.Valid == true;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// If the activated display key matches a revoked or transferred-away row in <c>issued_licenses</c>,
    /// callers must treat the deployment as unpaid/expired.
    /// </summary>
    private bool IsPersistedLicenseKeyBlockedInRegistry(string normalizedLicenseKeyUpper)
    {
        try
        {
            using var db = _dbContextFactory.CreateDbContext();
            return db.IssuedLicenses.AsNoTracking().Any(il =>
                EF.Functions.ILike(il.LicenseKey, normalizedLicenseKeyUpper)
                && (il.IsRevoked || il.TransferredToLicenseId != null));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "License: could not evaluate issued_licenses registry block; ignoring overlay.");
            return false;
        }
    }

    private bool TryVerifyOfflineJwt(string jwt, string expectedLicenseKey, out string? error)
    {
        error = null;
        var pem = _options.Value.OfflineVerificationPublicKeyPem?.Trim();
        if (string.IsNullOrEmpty(pem))
        {
            error = "Public key missing.";
            return false;
        }

        var parts = jwt.Split('.');
        if (parts.Length != 3)
        {
            error = "JWT must have three segments.";
            return false;
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            var rsaKey = new RsaSecurityKey(rsa.ExportParameters(false));

            var opts = _options.Value;
            var parms = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = rsaKey,
                ValidateIssuer = !string.IsNullOrWhiteSpace(opts.LicenseJwtIssuer),
                ValidIssuer = opts.LicenseJwtIssuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(opts.LicenseJwtAudience),
                ValidAudience = opts.LicenseJwtAudience,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
                RequireSignedTokens = true,
            };

            var handler = new JwtSecurityTokenHandler();
            handler.InboundClaimTypeMap.Clear();

            try
            {
                handler.ValidateToken(jwt, parms, out _);
            }
            catch (SecurityTokenException ex)
            {
                error = ex.Message;
                return false;
            }

            var jwtToken = handler.ReadJwtToken(jwt);
            var licenseKeyClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "licenseKey")?.Value;
            if (string.IsNullOrEmpty(licenseKeyClaim))
            {
                error = "licenseKey claim missing.";
                return false;
            }

            if (!string.Equals(licenseKeyClaim, expectedLicenseKey, StringComparison.OrdinalIgnoreCase))
            {
                error = "licenseKey mismatch.";
                return false;
            }

            var boundHash = jwtToken.Claims.FirstOrDefault(c => c.Type == "machineHash")?.Value;
            var licenseIsFloating = string.IsNullOrEmpty(boundHash)
                || string.Equals(boundHash, "FLOATING", StringComparison.OrdinalIgnoreCase);

            if (!licenseIsFloating)
            {
                var requireBinding = _options.Value.RequireMachineBinding;
                if (!requireBinding)
                {
                    _logger.LogWarning(
                        "License: machineHash binding bypassed because License:RequireMachineBinding=false. License is portable across machines. BoundHashPrefix={BoundPrefix} LocalHashPrefix={LocalPrefix}",
                        SafePrefix(boundHash!),
                        SafePrefix(_storage.MachineHashHex));
                }
                else if (!string.Equals(boundHash, _storage.MachineHashHex, StringComparison.OrdinalIgnoreCase))
                {
                    error = "Machine hash mismatch.";
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            error = "Invalid JWT: " + ex.Message;
            return false;
        }
    }

    private LicensePersistedState LoadOrCreatePersisted()
    {
        var loaded = _storage.LoadLicenseFromFile();
        if (loaded is not null)
            return loaded;

        var fresh = new LicensePersistedState { FirstRunUtc = DateTime.UtcNow };
        _storage.SaveLicenseToFile(fresh);
        _logger.LogInformation("License: first run recorded at {FirstRun:o} (UTC) at {Path}.",
            fresh.FirstRunUtc, _storage.LicenseFilePath);
        return fresh;
    }

    private static byte[] Base64UrlDecode(string segment)
    {
        var s = segment.Replace("-", "+", StringComparison.Ordinal).Replace("_", "/", StringComparison.Ordinal);
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }

        return Convert.FromBase64String(s);
    }

    private static string SafePrefix(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "(empty)";
        return value.Length <= 12 ? value : value[..12] + "…";
    }

    private sealed record RemoteLicenseRequest(string MachineHash, string LicenseKey);

    private sealed record RemoteLicenseResponseDto(bool Valid);
}
