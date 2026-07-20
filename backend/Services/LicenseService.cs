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
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.License;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KasseAPI_Final.Services;

/// <summary>License evaluation for on-premise deployments (trial, offline JWS, optional remote check).
/// Persistence: <see cref="ILicenseStorageService"/> (encrypted file) plus <c>activated_licenses</c> rows for activation audit and restart recovery.</summary>
public interface ILicenseService
{
    /// <summary>Load state, evaluate trial/license, log warnings. Call once per process startup.</summary>
    void EvaluateOnStartup();

    LicenseStatusResponse GetStatus();
    LicenseStatusResponse GetDeploymentStatus();

    /// <summary>Rebuilds license snapshot from <c>activated_licenses</c> (authoritative paid row) plus on-disk trial/JWT state, then returns the same overlays as <see cref="GetStatus"/>.</summary>
    Task<LicenseStatusResponse> GetCurrentStatusAsync(CancellationToken cancellationToken = default);
    Task<LicenseStatusResponse> GetCurrentDeploymentStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>True only after <see cref="EvaluateOnStartup"/> has produced a real snapshot (used by header/visibility middleware to distinguish "None" from "Expired").</summary>
    bool IsLicenseSnapshotInitialized { get; }

    /// <summary>Lightweight async validation used by <see cref="Middleware.LicenseMiddleware"/> (snapshot + revocation overlay).</summary>
    Task<LicenseValidationResult> ValidateAsync(CancellationToken cancellationToken = default);

    Task<LicenseActivationResult> ActivateAsync(
        ActivateLicenseRequest request,
        LicenseActivationClientInfo? clientInfo = null,
        CancellationToken cancellationToken = default);

    /// <summary>Mandant license snapshot for a tenant row (grace period, access flags, German status copy).</summary>
    Task<LicenseStatusInfo> GetLicenseStatusAsync(Guid tenantId, CancellationToken cancellationToken = default);
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
    IReadOnlyList<LicenseReminderNotice>? Reminders = null,
    IReadOnlyList<string>? EnabledFeatures = null,
    bool IsDevelopmentBypass = false);

/// <summary>Operational access tier for mandant licenses after expiry (grace and optional read-only window).</summary>
public enum LicenseAccessLevel
{
    /// <summary>License valid or within the post-expiry grace period.</summary>
    FullAccess,

    /// <summary>Grace period ended; view/export allowed, transactions blocked (when configured).</summary>
    ReadOnly,

    /// <summary>Access denied (lockdown or missing license).</summary>
    Blocked,
}

/// <summary>Rich mandant license snapshot including grace-period math (see <see cref="LicenseGracePeriodConfig"/>).</summary>
public sealed class LicenseStatusInfo
{
    public bool IsActive { get; set; }
    /// <summary>True when <see cref="ValidUntil"/> is in the past (includes grace and lockdown).</summary>
    public bool IsExpired { get; set; }
    public DateTime? ValidUntil { get; set; }
    public int DaysRemaining { get; set; }
    /// <summary>Positive when <see cref="ValidUntil"/> is in the past.</summary>
    public int DaysOverdue { get; set; }
    public bool IsInGracePeriod { get; set; }
    /// <summary>True when expired past the grace window (POS locked; Super Admin unlock only).</summary>
    public bool IsLocked { get; set; }
    public int GracePeriodRemaining { get; set; }
    /// <summary>UTC date when POS lock starts (expiry + grace days); null when not expired.</summary>
    public DateTime? LockDate { get; set; }
    public bool CanAccess { get; set; }
    public bool CanTransact { get; set; }
    /// <summary>Stable key for clients (<see cref="LicenseStatusMessageKeys"/>).</summary>
    public string StatusMessageKey { get; set; } = string.Empty;
    /// <summary>Localized status copy (default language de when built without Accept-Language).</summary>
    public string StatusMessage { get; set; } = string.Empty;
    /// <summary>Restriction codes (<see cref="LicenseStatusRestrictionCodes"/>).</summary>
    public IReadOnlyList<string> Restrictions { get; set; } = Array.Empty<string>();
    public bool RequiresRenewal { get; set; }
}

/// <summary>Builds <see cref="LicenseStatusInfo"/> from tenant <see cref="Models.Tenant.LicenseValidUntilUtc"/>.</summary>
public static class LicenseStatusInfoBuilder
{
    private static readonly TenantLicenseValidator Validator = new();

    public static LicenseStatusInfo Build(
        DateTime? validUntilUtc,
        bool isSuperAdmin = false,
        DateTime? nowUtc = null,
        string? language = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        if (!validUntilUtc.HasValue)
        {
            return new LicenseStatusInfo
            {
                IsActive = false,
                IsExpired = true,
                ValidUntil = null,
                DaysRemaining = 0,
                DaysOverdue = 0,
                IsInGracePeriod = false,
                IsLocked = !isSuperAdmin,
                GracePeriodRemaining = 0,
                LockDate = null,
                CanAccess = isSuperAdmin,
                CanTransact = isSuperAdmin,
                StatusMessageKey = LicenseStatusMessageKeys.None,
                StatusMessage = LicenseStatusMessages.Format(LicenseStatusMessageKeys.None, language),
                Restrictions = LicenseStatusMessages.GetRestrictions(isExpired: true, isGracePeriod: false, isLocked: !isSuperAdmin),
                RequiresRenewal = true,
            };
        }

        var until = DateTime.SpecifyKind(validUntilUtc.Value, DateTimeKind.Utc);
        var daysOverdue = Math.Max(0, (now - until).Days);
        var daysRemaining = daysOverdue == 0
            ? Math.Max(0, (int)Math.Ceiling((until - now).TotalDays))
            : 0;
        var isActive = daysOverdue == 0;
        var isExpired = !isActive;
        var tenantStatus = Validator.GetStatus(until, now);
        var permissions = Validator.GetPermissions(until, isSuperAdmin, now);
        var accessLevel = MapAccessLevel(tenantStatus, isSuperAdmin);
        var isInGrace = tenantStatus == TenantLicenseStatus.GraceWrite;
        var isLocked = tenantStatus is (TenantLicenseStatus.Lockdown or TenantLicenseStatus.Archived)
            && !isSuperAdmin;
        var graceRemaining = isInGrace
            ? Math.Max(0, LicenseGracePeriodConfig.GracePeriodDays - daysOverdue)
            : 0;
        var lockDate = LicenseStatusMessages.ComputeLockDate(until, isExpired);
        var messageKey = ResolveMessageKey(accessLevel, daysRemaining, daysOverdue, isInGrace, isLocked);
        var restrictions = LicenseStatusMessages.GetRestrictions(isExpired, isInGrace, isLocked);

        return new LicenseStatusInfo
        {
            IsActive = isActive,
            IsExpired = isExpired,
            ValidUntil = until,
            DaysRemaining = daysRemaining,
            DaysOverdue = daysOverdue,
            IsInGracePeriod = isInGrace,
            IsLocked = isLocked,
            GracePeriodRemaining = graceRemaining,
            LockDate = lockDate,
            CanAccess = permissions.CanAccess,
            CanTransact = permissions.CanWrite,
            StatusMessageKey = messageKey,
            StatusMessage = LicenseStatusMessages.Format(
                messageKey,
                language,
                daysRemaining,
                daysOverdue,
                graceRemaining,
                lockDate),
            Restrictions = restrictions,
            RequiresRenewal = tenantStatus is TenantLicenseStatus.Lockdown or TenantLicenseStatus.Archived,
        };
    }

    private static string ResolveMessageKey(
        LicenseAccessLevel accessLevel,
        int daysRemaining,
        int daysOverdue,
        bool isInGrace,
        bool isLocked)
    {
        if (isLocked || accessLevel == LicenseAccessLevel.Blocked)
            return daysOverdue > 0 ? LicenseStatusMessageKeys.Locked : LicenseStatusMessageKeys.None;

        if (isInGrace || (accessLevel == LicenseAccessLevel.FullAccess && daysOverdue > 0))
            return LicenseStatusMessageKeys.Grace;

        if (accessLevel == LicenseAccessLevel.FullAccess
            && daysOverdue == 0
            && daysRemaining <= LicenseGracePeriodConfig.WarningDaysBeforeExpiry)
            return LicenseStatusMessageKeys.ExpiringSoon;

        if (accessLevel == LicenseAccessLevel.ReadOnly)
            return LicenseStatusMessageKeys.Locked;

        return LicenseStatusMessageKeys.Active;
    }

    private static LicenseAccessLevel MapAccessLevel(
        TenantLicenseStatus status,
        bool isSuperAdmin)
    {
        if (isSuperAdmin)
            return LicenseAccessLevel.FullAccess;

        return status switch
        {
            TenantLicenseStatus.Active or TenantLicenseStatus.GraceWrite
                => LicenseAccessLevel.FullAccess,
            TenantLicenseStatus.GraceReadOnly => LicenseAccessLevel.ReadOnly,
            TenantLicenseStatus.Lockdown or TenantLicenseStatus.Archived or TenantLicenseStatus.NoLicense
                => LicenseAccessLevel.Blocked,
            _ => LicenseAccessLevel.Blocked,
        };
    }
}

public sealed class ActivateLicenseRequest
{
    [Required]
    public string LicenseKey { get; set; } = "";

    public string? OfflineActivationJwt { get; set; }

    /// <summary>Optional client-reported fingerprint; when set, must match this host.</summary>
    [JsonPropertyName("machineFingerprint")]
    public string? MachineFingerprint { get; set; }
}

public sealed record LicenseActivationResult(
    bool Success,
    string? Message,
    DateTime? ValidUntil = null,
    string? LicenseType = null,
    Guid? TenantId = null,
    string? TenantSlug = null,
    string? ApiBaseUrl = null);

/// <summary>Optional HTTP client metadata stored on each activation audit row.</summary>
public sealed record LicenseActivationClientInfo(
    string? ClientIp,
    string? UserAgent,
    Guid? InitiatingUserId = null,
    string? SourceAppContext = null);

/// <summary>
/// Lisans iş kurallarını yürütür; saklama (şifreli dosya) tamamen <see cref="ILicenseStorageService"/>'e delege edilir.
/// Registry/DPAPI/Windows'a özgü kod yoktur — Windows, Linux ve macOS üzerinde aynı şekilde çalışır.
/// </summary>
public sealed class LicenseService : ILicenseService
{
    public const int TrialDays = 30;

    private static int GracePeriodDays => LicenseGracePeriodConfig.GracePeriodDays;
    private static int WarningDaysBeforeExpiry => LicenseGracePeriodConfig.WarningDaysBeforeExpiry;

    /// <summary>
    /// Paid window granted for key-only activation (no JWT / no remote / no local issuance row).
    /// REGK display keys do not embed a reversible expiry without the signed JWT.
    /// </summary>
    private const int KeyOnlyPaidActivationDays = 365;

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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IActivityEventPublisher _activityPublisher;
    private readonly ILogger<LicenseService> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly TseOptions _tseOptions;
    private readonly IOptionsMonitor<DevelopmentOptions> _developmentOptions;
    private readonly IDevelopmentModeService _developmentModeService;

    private readonly object _gate = new();
    private LicenseStatusResponse _snapshot = new(false, false, false, 0, null, "");
    private LicensePersistedState? _persisted;
    private bool _snapshotInitialized;

    public LicenseService(
        IOptions<LicenseOptions> options,
        IHttpClientFactory httpClientFactory,
        ILicenseStorageService storage,
        IServiceScopeFactory scopeFactory,
        IActivityEventPublisher activityPublisher,
        ILogger<LicenseService> logger,
        IHostEnvironment hostEnvironment,
        IOptions<TseOptions> tseOptions,
        IOptionsMonitor<DevelopmentOptions> developmentOptions,
        IDevelopmentModeService developmentModeService)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _storage = storage;
        _scopeFactory = scopeFactory;
        _activityPublisher = activityPublisher;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
        _tseOptions = tseOptions.Value;
        _developmentOptions = developmentOptions;
        _developmentModeService = developmentModeService;
    }

    private T WithDbContext<T>(Func<AppDbContext, T> action)
    {
        using var scope = _scopeFactory.CreateScope();
        using var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return action(db);
    }

    private async Task WithDbContextAsync(
        Func<AppDbContext, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> WithDbContextAsync<T>(
        Func<AppDbContext, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db, cancellationToken).ConfigureAwait(false);
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
                _snapshot = new LicenseStatusResponse(
                    true,
                    false,
                    false,
                    0,
                    null,
                    "openapi-export",
                    EnabledFeatures: LicenseFeatureIds.All,
                    IsDevelopmentBypass: false);
                _snapshotInitialized = true;
            }
            return;
        }

        ActivatedLicense? dbActivation = null;
        try
        {
            dbActivation = WithDbContext(QueryPrimaryActiveActivation);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "License: startup could not read activated_licenses for primary activation.");
        }

        lock (_gate)
        {
            _persisted = LoadOrCreatePersisted();
            ApplyPrimaryActiveActivationRowToPersisted(dbActivation);
            if (string.IsNullOrWhiteSpace(_persisted.LicenseKey))
            {
                var restoredKey = TryRestoreLicenseKeyFromActivatedLicenses();
                if (!string.IsNullOrWhiteSpace(restoredKey))
                {
                    _persisted.LicenseKey = restoredKey.Trim().ToUpperInvariant();
                    _storage.SaveLicenseToFile(_persisted);
                }
            }

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

        TryPublishLicenseActivityFireAndForget(_snapshot);
    }

    private void TryPublishLicenseActivityFireAndForget(LicenseStatusResponse snapshot)
    {
        if (OpenApiExportMode.IsEnabled || snapshot.IsDevelopmentBypass)
            return;

        _ = PublishLicenseActivityAsync(snapshot);
    }

    private async Task PublishLicenseActivityAsync(LicenseStatusResponse snapshot)
    {
        try
        {
            if (snapshot.IsExpired)
            {
                await _activityPublisher.TryPublishAsync(
                    LegacyDefaultTenantIds.Primary,
                    ActivityEventType.LicenseExpired,
                    new
                    {
                        DaysRemaining = 0,
                        ExpiryDate = snapshot.ExpiryDate,
                        TenantId = LegacyDefaultTenantIds.Primary,
                    },
                    dedupKey: "deployment_license_expired").ConfigureAwait(false);
                return;
            }

            if (snapshot.ExpiryDate == null || snapshot.DaysRemaining > 30)
                return;

            await _activityPublisher.TryPublishAsync(
                LegacyDefaultTenantIds.Primary,
                ActivityEventType.LicenseExpiringSoon,
                new
                {
                    DaysRemaining = snapshot.DaysRemaining,
                    ExpiryDate = snapshot.ExpiryDate,
                    TenantId = LegacyDefaultTenantIds.Primary,
                },
                dedupKey: $"deployment_license_expiry_{snapshot.DaysRemaining}d").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Deployment license activity notification publish skipped");
        }
    }

    public LicenseStatusResponse GetStatus()
    {
        string? persistedKeyForOverlays = null;
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
                persistedKeyForOverlays = _persisted.LicenseKey.Trim().ToUpperInvariant();
            }
        }

        var afterOverlays = ApplyLicenseComplianceOverlays(snapshot, persistedKeyForOverlays);
        return ApplyDevelopmentLicenseBypassIfNeeded(afterOverlays);
    }

    public LicenseStatusResponse GetDeploymentStatus() => GetStatus();

    public async Task<LicenseStatusResponse> GetCurrentStatusAsync(CancellationToken cancellationToken = default)
    {
        if (OpenApiExportMode.IsEnabled)
        {
            return new LicenseStatusResponse(
                true,
                false,
                false,
                0,
                null,
                _storage.MachineHashHex,
                EnabledFeatures: LicenseFeatureIds.All,
                IsDevelopmentBypass: false);
        }

        ActivatedLicense? dbActivation = null;
        try
        {
            dbActivation = await WithDbContextAsync(
                QueryPrimaryActiveActivationAsync,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "License: GetCurrentStatusAsync database read failed; using in-memory snapshot only.");
        }

        LicenseStatusResponse snapshot;
        string? persistedKeyForOverlays;
        lock (_gate)
        {
            _persisted ??= LoadOrCreatePersisted();
            if (dbActivation != null)
                ApplyPrimaryActiveActivationRowToPersisted(dbActivation);

            var paid = TryValidatePaidLicense(_persisted);
            _snapshot = BuildSnapshot(_persisted, paid);
            _snapshotInitialized = true;
            snapshot = _snapshot;
            persistedKeyForOverlays = !string.IsNullOrWhiteSpace(_persisted.LicenseKey)
                ? _persisted.LicenseKey.Trim().ToUpperInvariant()
                : null;
        }

        var afterOverlays = ApplyLicenseComplianceOverlays(snapshot, persistedKeyForOverlays);
        return ApplyDevelopmentLicenseBypassIfNeeded(afterOverlays);
    }

    public Task<LicenseStatusResponse> GetCurrentDeploymentStatusAsync(CancellationToken cancellationToken = default) =>
        GetCurrentStatusAsync(cancellationToken);

    public async Task<LicenseValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var s = await GetCurrentStatusAsync(cancellationToken).ConfigureAwait(false);
        var paid = s.IsValid && !s.IsTrial;
        var trialActive = s.IsTrial && !s.IsExpired;
        var operational = paid || trialActive;
        if (paid && !s.IsDevelopmentBypass)
            await TryRecordLicenseHeartbeatAsync(cancellationToken).ConfigureAwait(false);

        return new LicenseValidationResult
        {
            IsLicenseOperational = operational,
            IsTrial = s.IsTrial,
            IsExpired = s.IsExpired,
            IsPaidValid = paid,
            DaysRemaining = s.DaysRemaining,
            ExpiryUtc = s.ExpiryDate,
        };
    }

    /// <inheritdoc />
    public async Task<LicenseStatusInfo> GetLicenseStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (OpenApiExportMode.IsEnabled)
            return CreateUnlimitedMandantLicenseStatus("Aktive Lizenz");

        if (LicenseEnforcementPolicy.ShouldDisableEnforcement(
                _hostEnvironment,
                _tseOptions,
                _developmentModeService,
                _options.Value))
        {
            // Development: expose persisted mandant row for FA/POS status UI (QA scenarios).
            // Middleware still skips enforcement when policy is disabled.
            if (_hostEnvironment.IsDevelopment())
            {
                return await WithDbContextAsync(async (db, ct) =>
                {
                    var tenant = await db.Tenants
                        .AsNoTracking()
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
                        .ConfigureAwait(false);

                    if (tenant == null)
                        return new LicenseStatusInfo { CanAccess = false };

                    return BuildTenantLicenseStatusInfo(tenant);
                }, cancellationToken).ConfigureAwait(false);
            }

            return CreateUnlimitedMandantLicenseStatus("Demo Mode - Unlimited Access");
        }

        return await WithDbContextAsync(async (db, ct) =>
        {
            var tenant = await db.Tenants
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
                .ConfigureAwait(false);

            if (tenant == null)
                return new LicenseStatusInfo { CanAccess = false };

            return BuildTenantLicenseStatusInfo(tenant);
        }, cancellationToken).ConfigureAwait(false);
    }

    internal static LicenseStatusInfo CreateUnlimitedMandantLicenseStatus(string statusMessage) =>
        new()
        {
            IsActive = true,
            IsExpired = false,
            CanAccess = true,
            CanTransact = true,
            DaysRemaining = 999,
            DaysOverdue = 0,
            IsInGracePeriod = false,
            IsLocked = false,
            GracePeriodRemaining = 0,
            LockDate = null,
            RequiresRenewal = false,
            StatusMessageKey = LicenseStatusMessageKeys.Active,
            StatusMessage = statusMessage,
            Restrictions = Array.Empty<string>(),
        };

    /// <summary>
    /// Builds mandant license status from the tenant row.
    /// Preserves full <see cref="Tenant.LicenseValidUntilUtc"/> (no UTC-midnight truncation)
    /// and uses ceiling day math for remaining time (same as <see cref="Tenancy.TenantLicenseStatusMapper"/>).
    /// </summary>
    internal static LicenseStatusInfo BuildTenantLicenseStatusInfo(
        Tenant tenant,
        DateTime? nowUtc = null,
        string? language = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var lang = language ?? "de";

        if (!tenant.LicenseValidUntilUtc.HasValue)
        {
            return new LicenseStatusInfo
            {
                IsActive = tenant.IsActive,
                IsExpired = false,
                CanAccess = true,
                CanTransact = true,
                DaysRemaining = 999,
                DaysOverdue = 0,
                IsInGracePeriod = false,
                IsLocked = false,
                GracePeriodRemaining = 0,
                LockDate = null,
                StatusMessageKey = LicenseStatusMessageKeys.Active,
                StatusMessage = LicenseStatusMessages.Format(LicenseStatusMessageKeys.Active, lang),
                Restrictions = Array.Empty<string>(),
                RequiresRenewal = false,
            };
        }

        var validUntil = DateTime.SpecifyKind(tenant.LicenseValidUntilUtc.Value, DateTimeKind.Utc);
        var isExpired = now >= validUntil;
        int daysRemaining;
        int daysOverdue;
        if (isExpired)
        {
            // Match LicenseStatusInfoBuilder / TenantLicenseValidator: whole elapsed days via TimeSpan.Days.
            daysOverdue = Math.Max(0, (now - validUntil).Days);
            if (daysOverdue == 0)
                daysOverdue = 1;
            daysRemaining = -daysOverdue;
        }
        else
        {
            daysRemaining = (int)Math.Ceiling((validUntil - now).TotalDays);
            daysOverdue = 0;
        }

        var isInGracePeriod = isExpired && daysOverdue <= GracePeriodDays;
        var isLocked = isExpired && daysOverdue > GracePeriodDays;
        var gracePeriodRemaining = isInGracePeriod ? GracePeriodDays - daysOverdue : 0;
        var canAccess = !isExpired || isInGracePeriod;
        var canTransact = canAccess;
        var lockDate = LicenseStatusMessages.ComputeLockDate(validUntil, isExpired);
        var restrictions = LicenseStatusMessages.GetRestrictions(isExpired, isInGracePeriod, isLocked);

        string messageKey;
        if (!isExpired)
        {
            messageKey = daysRemaining <= WarningDaysBeforeExpiry
                ? LicenseStatusMessageKeys.ExpiringSoon
                : LicenseStatusMessageKeys.Active;
        }
        else if (isInGracePeriod)
        {
            messageKey = LicenseStatusMessageKeys.Grace;
        }
        else
        {
            messageKey = LicenseStatusMessageKeys.Locked;
        }

        return new LicenseStatusInfo
        {
            IsActive = tenant.IsActive && !isExpired,
            IsExpired = isExpired,
            ValidUntil = validUntil,
            DaysRemaining = daysRemaining,
            DaysOverdue = daysOverdue,
            IsInGracePeriod = isInGracePeriod,
            IsLocked = isLocked,
            GracePeriodRemaining = gracePeriodRemaining,
            LockDate = lockDate,
            CanAccess = canAccess,
            CanTransact = canTransact,
            StatusMessageKey = messageKey,
            StatusMessage = LicenseStatusMessages.Format(
                messageKey,
                lang,
                daysRemaining: Math.Max(0, daysRemaining),
                daysOverdue: daysOverdue,
                gracePeriodRemaining: gracePeriodRemaining,
                lockDateUtc: lockDate),
            Restrictions = restrictions,
            RequiresRenewal = isLocked,
        };
    }

    /// <summary>
    /// Bumps <c>last_seen_at_utc</c> for this host's paid activation row (throttled) so admin reporting reflects POS/FA traffic.
    /// </summary>
    private async Task TryRecordLicenseHeartbeatAsync(CancellationToken cancellationToken)
    {
        if (OpenApiExportMode.IsEnabled)
            return;

        string? normalizedKeyUpper;
        lock (_gate)
        {
            var key = _persisted?.LicenseKey;
            if (string.IsNullOrWhiteSpace(key))
                return;
            normalizedKeyUpper = key.Trim().ToUpperInvariant();
        }

        if (!LicenseKeyRegex.IsMatch(normalizedKeyUpper))
            return;

        var machine = _storage.MachineHashHex;
        var utcNow = DateTime.UtcNow;
        var throttleCutoff = utcNow.AddMinutes(-2);

        try
        {
            await WithDbContextAsync(async (db, ct) =>
            {
                await db.ActivatedLicenses
                    .Where(a =>
                        a.IsActive
                        && a.MachineFingerprint == machine
                        && EF.Functions.ILike(a.LicenseKey, normalizedKeyUpper)
                        && a.LastSeenAtUtc < throttleCutoff)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(a => a.LastSeenAtUtc, utcNow),
                        ct)
                    .ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "License: last_seen heartbeat update skipped or failed.");
        }
    }

    public async Task<LicenseActivationResult> ActivateAsync(
        ActivateLicenseRequest request,
        LicenseActivationClientInfo? clientInfo = null,
        CancellationToken cancellationToken = default)
    {
        var fpHost = _storage.MachineHashHex;

        async Task<LicenseActivationResult> FailAndLogAsync(string licenseKeyForDb, string message)
        {
            await TryInsertActivationAttemptAsync(
                ClipDbField(licenseKeyForDb, 64),
                ClipDbField(fpHost, 128),
                LicenseActivationAttemptStatus.Failed,
                message,
                clientInfo,
                cancellationToken).ConfigureAwait(false);
            return new LicenseActivationResult(false, message, null, null);
        }

        if (OpenApiExportMode.IsEnabled)
            return await FailAndLogAsync("", "License activation is disabled in OpenAPI export mode.");

        if (request is null || string.IsNullOrWhiteSpace(request.LicenseKey))
            return await FailAndLogAsync(
                ClipDbField(request?.LicenseKey?.Trim().ToUpperInvariant() ?? "", 64),
                "LicenseKey is required.");

        var normalizedKey = request.LicenseKey.Trim().ToUpperInvariant();
        if (!LicenseKeyRegex.IsMatch(normalizedKey))
            return await FailAndLogAsync(normalizedKey, "Invalid license key format. Expected REGK-XXXXX-XXXXX-XXXXX.");

        if (!string.IsNullOrWhiteSpace(request.MachineFingerprint))
        {
            var fp = request.MachineFingerprint.Trim();
            if (!string.Equals(fp, fpHost, StringComparison.OrdinalIgnoreCase))
                return await FailAndLogAsync(normalizedKey, "Machine fingerprint does not match this host.");
        }

        lock (_gate)
        {
            _persisted ??= LoadOrCreatePersisted();
        }

        var opts = _options.Value;
        var offlineJwt = request.OfflineActivationJwt?.Trim();
        DateTime? keyOnlyPaidValidUntilUtc = null;

        try
        {
            if (!string.IsNullOrEmpty(offlineJwt))
            {
                if (string.IsNullOrWhiteSpace(opts.OfflineVerificationPublicKeyPem))
                    return await FailAndLogAsync(normalizedKey, "OfflineVerificationPublicKeyPem is not configured.");

                _logger.LogInformation(
                    "License activation: offline JWT verification starting. LicenseKeyPrefix={LicenseKeyPrefix}, JwtLength={JwtLength}, PemConfigured={PemConfigured}, ValidateIssuer={ValidateIssuer}, ValidateAudience={ValidateAudience}, RequireMachineBinding={RequireMachineBinding}",
                    SafePrefix(normalizedKey),
                    offlineJwt.Length,
                    true,
                    !string.IsNullOrWhiteSpace(opts.LicenseJwtIssuer),
                    !string.IsNullOrWhiteSpace(opts.LicenseJwtAudience),
                    opts.RequireMachineBinding);

                if (!TryVerifyOfflineJwt(offlineJwt, normalizedKey, out var err))
                    return await FailAndLogAsync(normalizedKey, err ?? "Offline JWT verification failed.");

                if (IsPersistedLicenseKeyBlockedInRegistry(normalizedKey))
                {
                    return await FailAndLogAsync(
                        normalizedKey,
                        "This license key was transferred or revoked. Activate using the replacement key provided by support.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(opts.RemoteValidationUrl))
            {
                var remoteOk = await ValidateRemoteAsync(normalizedKey, cancellationToken).ConfigureAwait(false);
                if (!remoteOk)
                    return await FailAndLogAsync(normalizedKey, "Remote license server did not accept this key for this machine.");

                if (IsPersistedLicenseKeyBlockedInRegistry(normalizedKey))
                {
                    return await FailAndLogAsync(
                        normalizedKey,
                        "This license key was transferred or revoked. Activate using the replacement key provided by support.");
                }
            }
            else
            {
                // Same-host fallback: licence was issued on this deployment (issued_licenses).
                // Honour the form "(optional) Offline-Aktivierungs-JWT" contract — if the row exists for this host
                // (non-revoked, non-transferred, non-superseded, non-expired, fingerprint-bound when required),
                // accept activation without remote validation. Mirrors the snapshot logic in TryValidatePaidLicense.
                if (TryResolveIssuedRegistryPaid(normalizedKey, out _))
                {
                    _logger.LogInformation(
                        "License activation: same-host issued_licenses match accepted (no JWT, no remote). LicenseKeyPrefix={LicenseKeyPrefix}",
                        SafePrefix(normalizedKey));
                }
                else if (IsKeyOnlyOfflineActivationContextAllowed())
                {
                    keyOnlyPaidValidUntilUtc = DateTime.SpecifyKind(
                        DateTime.UtcNow.AddDays(KeyOnlyPaidActivationDays),
                        DateTimeKind.Utc);
                    _logger.LogInformation(
                        "License activation: key-only mode (Development or License:AllowKeyOnlyOfflineActivation). LicenseKeyPrefix={LicenseKeyPrefix}, PaidValidUntilUtc={ValidUntil:o}",
                        SafePrefix(normalizedKey),
                        keyOnlyPaidValidUntilUtc);
                }
                else
                {
                    return await FailAndLogAsync(
                        normalizedKey,
                        "Provide OfflineActivationJwt (offline) or configure License:RemoteValidationUrl (online), or enable License:AllowKeyOnlyOfflineActivation for key-only SMB setups.");
                }
            }

            lock (_gate)
            {
                _persisted ??= new LicensePersistedState { FirstRunUtc = DateTime.UtcNow };
                _persisted.LicenseKey = normalizedKey;
                _persisted.OfflineJwt = offlineJwt;
                _persisted.KeyOnlyPaidValidUntilUtc = string.IsNullOrEmpty(offlineJwt)
                    ? keyOnlyPaidValidUntilUtc
                    : null;
                _storage.SaveLicenseToFile(_persisted);
                var paid = TryValidatePaidLicense(_persisted);
                _snapshot = BuildSnapshot(_persisted, paid);
                _snapshotInitialized = true;
            }

            var st = GetStatus();
            var activationFeaturesJson = LicenseFeatureIds.SerializeJsonArray(
                ResolveFeaturesForActivationUpsert(normalizedKey, offlineJwt));
            try
            {
                await UpsertActivatedLicenseAsync(
                        normalizedKey,
                        st.ExpiryDate,
                        activationFeaturesJson,
                        clientInfo,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "License: activation file succeeded but activated_licenses DB upsert failed.");
                return await FailAndLogAsync(
                    normalizedKey,
                    "License state was saved but activation could not be recorded in the database. Check server logs.");
            }

            await GetCurrentStatusAsync(cancellationToken).ConfigureAwait(false);

            await TryInsertActivationAttemptAsync(
                normalizedKey,
                ClipDbField(fpHost, 128),
                LicenseActivationAttemptStatus.Success,
                failureReason: null,
                clientInfo,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("License: activation succeeded for key prefix {Prefix}.", SafePrefix(normalizedKey));
            var licenseType = MapPublicLicenseTypeLabel(st);
            return new LicenseActivationResult(true, "Lizenz erfolgreich aktiviert", st.ExpiryDate, licenseType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "License: activation failed with unexpected error.");
            return await FailAndLogAsync(normalizedKey, "Activation failed due to an internal error.");
        }
    }

    /// <summary>Maps internal snapshot to the same coarse labels as <c>GET /api/license/status</c> (<c>LicenseType</c> field).</summary>
    private static string MapPublicLicenseTypeLabel(LicenseStatusResponse s)
    {
        var paid = s.IsValid && !s.IsTrial;
        var trialActive = s.IsTrial && !s.IsExpired;
        if (paid)
            return "Licensed";
        if (trialActive)
            return "Trial";
        return "Expired";
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
            // Paid: JWT exp, then activated_licenses (this host), then issued_licenses, else UI sentinel.
            DateTime? activatedExpiryUtc = null;
            DateTime? registryExpiryUtc = null;
            var keyUpper = !string.IsNullOrWhiteSpace(blob.LicenseKey)
                ? blob.LicenseKey.Trim().ToUpperInvariant()
                : "";
            if (jwtExpUtc is null && !string.IsNullOrWhiteSpace(blob.LicenseKey))
            {
                var k = keyUpper;
                if (LicenseKeyRegex.IsMatch(k))
                {
                    TryResolveActivatedDbPaid(k, out activatedExpiryUtc);
                    TryResolveIssuedRegistryPaid(k, out registryExpiryUtc);
                }
            }

            DateTime? keyOnlyExpiryUtc = null;
            if (blob.KeyOnlyPaidValidUntilUtc is { } koUntil && koUntil > nowUtc)
                keyOnlyExpiryUtc = DateTime.SpecifyKind(koUntil, DateTimeKind.Utc);

            var expiryUtc = jwtExpUtc ?? activatedExpiryUtc ?? registryExpiryUtc ?? keyOnlyExpiryUtc;
            var days = expiryUtc.HasValue
                ? Math.Max(0, (int)Math.Ceiling((expiryUtc.Value - nowUtc).TotalDays))
                : 365;
            var paidFeatures = !string.IsNullOrEmpty(keyUpper) && LicenseKeyRegex.IsMatch(keyUpper)
                ? ResolveEnabledFeaturesForPaid(keyUpper, blob)
                : LicenseFeatureIds.All;
            return new LicenseStatusResponse(
                IsValid: true,
                IsTrial: false,
                IsExpired: false,
                DaysRemaining: days,
                ExpiryDate: expiryUtc,
                MachineHash: _storage.MachineHashHex,
                EnabledFeatures: paidFeatures);
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
                MachineHash: _storage.MachineHashHex,
                EnabledFeatures: LicenseFeatureIds.All);
        }

        // Hem trial bitmiş hem geçerli ücretli lisans yok → expired.
        // ExpiryDate'i en son geçerli expiry olarak ver (paid varsa JWT exp, yoksa trial sonu).
        return new LicenseStatusResponse(
            IsValid: false,
            IsTrial: false,
            IsExpired: true,
            DaysRemaining: 0,
            ExpiryDate: jwtExpUtc ?? trialEndUtc,
            MachineHash: _storage.MachineHashHex,
            EnabledFeatures: Array.Empty<string>());
    }

    /// <summary>Resolves paid-mode feature flags: offline JWT payload, then activation row, then issuance row, else full bundle.</summary>
    private IReadOnlyList<string> ResolveEnabledFeaturesForPaid(string normalizedKeyUpper, LicensePersistedState blob)
    {
        var fromJwt = TryReadFeaturesFromLicenseJwtPayload(blob.OfflineJwt);
        if (fromJwt is { Count: > 0 })
            return fromJwt;

        var activatedJson = TryGetActivatedFeaturesJson(normalizedKeyUpper);
        var fromActivated = LicenseFeatureIds.TryParseStoredFeatures(activatedJson);
        if (fromActivated is { Count: > 0 })
            return fromActivated;

        var issuedJson = TryGetIssuedFeaturesJson(normalizedKeyUpper);
        var fromIssued = LicenseFeatureIds.TryParseStoredFeatures(issuedJson);
        if (fromIssued is { Count: > 0 })
            return fromIssued;

        return LicenseFeatureIds.All;
    }

    private IReadOnlyList<string>? TryReadFeaturesFromLicenseJwtPayload(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
            return null;
        try
        {
            var parts = jwt.Trim().Split('.');
            if (parts.Length != 3)
                return null;

            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty("features", out var featEl) || featEl.ValueKind != JsonValueKind.Array)
                return null;

            var list = new List<string>();
            foreach (var el in featEl.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.String)
                    continue;
                var s = el.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    list.Add(s.Trim());
            }

            return list.Count == 0 ? null : LicenseFeatureIds.Normalize(list);
        }
        catch
        {
            return null;
        }
    }

    private string? TryGetActivatedFeaturesJson(string normalizedKeyUpper)
    {
        try
        {
            var machine = _storage.MachineHashHex;
            return WithDbContext(db => db.ActivatedLicenses.AsNoTracking()
                .Where(a => a.IsActive && a.MachineFingerprint == machine && EF.Functions.ILike(a.LicenseKey, normalizedKeyUpper))
                .OrderByDescending(a => a.ActivatedAtUtc)
                .Select(a => a.FeaturesJson)
                .FirstOrDefault());
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "License: could not read activated_licenses.features_json.");
            return null;
        }
    }

    private string? TryGetIssuedFeaturesJson(string normalizedKeyUpper)
    {
        try
        {
            return WithDbContext(db => db.IssuedLicenses.AsNoTracking()
                .Where(il => EF.Functions.ILike(il.LicenseKey, normalizedKeyUpper))
                .Where(il => !il.IsDeleted && !il.IsCancelled)
                .Where(il => !il.IsRevoked && il.TransferredToLicenseId == null && il.SupersededByLicenseId == null)
                .OrderByDescending(il => il.IssuedAtUtc)
                .Select(il => il.FeaturesJson)
                .FirstOrDefault());
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "License: could not read issued_licenses.features_json.");
            return null;
        }
    }

    private IReadOnlyList<string> ResolveFeaturesForActivationUpsert(string normalizedKeyUpper, string? offlineJwt)
    {
        var issuedJson = TryGetIssuedFeaturesJson(normalizedKeyUpper);
        var fromIssued = LicenseFeatureIds.TryParseStoredFeatures(issuedJson);
        if (fromIssued is { Count: > 0 })
            return fromIssued;

        var fromJwt = TryReadFeaturesFromLicenseJwtPayload(offlineJwt);
        if (fromJwt is { Count: > 0 })
            return fromJwt;

        return LicenseFeatureIds.All;
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

        if (TryResolveActivatedDbPaid(key, out _))
            return true;

        // Same-host issuance: persisted key matches an active row (survives restarts without remote or JWT on disk).
        if (TryResolveIssuedRegistryPaid(key, out _))
            return true;

        if (TryValidateKeyOnlyPaidEntitlement(blob))
            return true;

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

    private bool IsKeyOnlyOfflineActivationContextAllowed()
        => _hostEnvironment.IsDevelopment() || _options.Value.AllowKeyOnlyOfflineActivation;

    private bool TryValidateKeyOnlyPaidEntitlement(LicensePersistedState blob)
    {
        if (blob.KeyOnlyPaidValidUntilUtc is not { } until || until <= DateTime.UtcNow)
            return false;
        return IsKeyOnlyOfflineActivationContextAllowed();
    }

    private ActivatedLicense? QueryPrimaryActiveActivation(AppDbContext db)
    {
        var machine = _storage.MachineHashHex;
        var now = DateTime.UtcNow;
        return db.ActivatedLicenses.AsNoTracking()
            .Where(a => a.IsActive && a.ValidUntilUtc > now)
            .Where(a => a.MachineFingerprint == null || a.MachineFingerprint == machine)
            .OrderByDescending(a => a.ActivatedAtUtc)
            .FirstOrDefault();
    }

    private Task<ActivatedLicense?> QueryPrimaryActiveActivationAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var machine = _storage.MachineHashHex;
        var now = DateTime.UtcNow;
        return db.ActivatedLicenses.AsNoTracking()
            .Where(a => a.IsActive && a.ValidUntilUtc > now)
            .Where(a => a.MachineFingerprint == null || a.MachineFingerprint == machine)
            .OrderByDescending(a => a.ActivatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private void ApplyPrimaryActiveActivationRowToPersisted(ActivatedLicense? row)
    {
        if (row is null || string.IsNullOrWhiteSpace(row.LicenseKey))
            return;

        _persisted ??= LoadOrCreatePersisted();

        var dbKey = row.LicenseKey.Trim().ToUpperInvariant();
        if (!LicenseKeyRegex.IsMatch(dbKey))
            return;

        var needsSave = false;
        if (!string.Equals(_persisted.LicenseKey, dbKey, StringComparison.OrdinalIgnoreCase))
        {
            _persisted.LicenseKey = dbKey;
            needsSave = true;
        }

        if (!string.IsNullOrEmpty(_persisted.OfflineJwt) && !LicenseJwtDisplayKeyMatches(_persisted.OfflineJwt.Trim(), dbKey))
        {
            _persisted.OfflineJwt = null;
            needsSave = true;
        }

        if (needsSave)
            _storage.SaveLicenseToFile(_persisted);
    }

    private static bool LicenseJwtDisplayKeyMatches(string jwt, string normalizedUpperKey)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3)
                return false;

            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty("licenseKey", out var lk) || lk.ValueKind != JsonValueKind.String)
                return false;

            var claim = lk.GetString();
            return string.Equals(claim?.Trim().ToUpperInvariant(), normalizedUpperKey, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// When development-mode DB bypass is active on a Development host, returns a synthetic paid snapshot for POS/admin guards.
    /// </summary>
    private LicenseStatusResponse ApplyDevelopmentLicenseBypassIfNeeded(LicenseStatusResponse snapshot)
    {
        if (OpenApiExportMode.IsEnabled
            || !LicenseEnforcementPolicy.ShouldDisableEnforcement(
                _hostEnvironment,
                _tseOptions,
                _developmentModeService,
                _options.Value))
            return snapshot;

        _logger.LogDebug("License enforcement bypass active for deployment status snapshot");

        var days = _developmentModeService.GetValidDays();
        var validUntil = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(days), DateTimeKind.Utc);
        var remaining = Math.Max(0, (int)Math.Ceiling((validUntil - DateTime.UtcNow).TotalDays));
        var feats = _developmentModeService.GetFeatures();
        var enabled = feats.Length > 0 ? (IReadOnlyList<string>)feats : LicenseFeatureIds.All;

        return new LicenseStatusResponse(
            IsValid: true,
            IsTrial: false,
            IsExpired: false,
            DaysRemaining: remaining,
            ExpiryDate: validUntil,
            MachineHash: snapshot.MachineHash,
            snapshot.Reminders,
            enabled,
            IsDevelopmentBypass: true);
    }

    private LicenseStatusResponse ApplyLicenseComplianceOverlays(LicenseStatusResponse snapshot, string? persistedKeyUpper)
    {
        if (!OpenApiExportMode.IsEnabled
            && persistedKeyUpper is not null
            && IsPersistedLicenseKeyBlockedInRegistry(persistedKeyUpper))
        {
            snapshot = snapshot with
            {
                IsValid = false,
                IsTrial = false,
                IsExpired = true,
                DaysRemaining = 0,
                EnabledFeatures = Array.Empty<string>(),
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
                EnabledFeatures = Array.Empty<string>(),
            };
        }

        return snapshot;
    }

    private string? TryRestoreLicenseKeyFromActivatedLicenses()
    {
        try
        {
            var machine = _storage.MachineHashHex;
            return WithDbContext(db => db.ActivatedLicenses.AsNoTracking()
                .Where(a => a.IsActive && a.ValidUntilUtc > DateTime.UtcNow)
                .Where(a => a.MachineFingerprint == null || a.MachineFingerprint == machine)
                .OrderByDescending(a => a.ActivatedAtUtc)
                .Select(a => a.LicenseKey)
                .FirstOrDefault());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "License: could not read activated_licenses for key restore.");
            return null;
        }
    }

    /// <summary>True when <c>activated_licenses</c> has a non-expired row for this machine and the persisted key.</summary>
    private bool TryResolveActivatedDbPaid(string normalizedKeyUpper, out DateTime? expiryUtc)
    {
        expiryUtc = null;
        try
        {
            var machine = _storage.MachineHashHex;
            var row = WithDbContext(db => db.ActivatedLicenses.AsNoTracking()
                .Where(a => a.IsActive && a.ValidUntilUtc > DateTime.UtcNow)
                .Where(a => a.MachineFingerprint == null || a.MachineFingerprint == machine)
                .Where(a => EF.Functions.ILike(a.LicenseKey, normalizedKeyUpper))
                .OrderByDescending(a => a.ActivatedAtUtc)
                .FirstOrDefault());

            if (row is null)
                return false;

            expiryUtc = DateTime.SpecifyKind(row.ValidUntilUtc, DateTimeKind.Utc);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "License: activated_licenses paid resolution failed.");
            return false;
        }
    }

    /// <summary>
    /// Resolves paid entitlement from <c>issued_licenses</c> when this deployment issued the key (on-prem registry).
    /// Ignores revoked, transferred-away, superseded rows and enforces fingerprint when required.
    /// </summary>
    private bool TryResolveIssuedRegistryPaid(string normalizedKeyUpper, out DateTime? expiryUtc)
    {
        expiryUtc = null;
        try
        {
            var row = WithDbContext(db => db.IssuedLicenses.AsNoTracking()
                .Where(il => EF.Functions.ILike(il.LicenseKey, normalizedKeyUpper))
                .Where(il => !il.IsDeleted && !il.IsCancelled)
                .Where(il => !il.IsRevoked && il.TransferredToLicenseId == null && il.SupersededByLicenseId == null)
                .OrderByDescending(il => il.IssuedAtUtc)
                .FirstOrDefault());

            if (row is null)
                return false;

            if (row.ExpiryAtUtc <= DateTime.UtcNow)
                return false;

            if (row.RequireFingerprint)
            {
                if (string.IsNullOrWhiteSpace(row.MachineHashHex))
                    return false;
                if (!string.Equals(row.MachineHashHex.Trim(), _storage.MachineHashHex, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            expiryUtc = DateTime.SpecifyKind(row.ExpiryAtUtc, DateTimeKind.Utc);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "License: issued_licenses paid resolution failed.");
            return false;
        }
    }

    private async Task UpsertActivatedLicenseAsync(
        string normalizedKeyUpper,
        DateTime? snapshotExpiryUtc,
        string featuresJson,
        LicenseActivationClientInfo? clientInfo,
        CancellationToken cancellationToken)
    {
        var machine = _storage.MachineHashHex;
        var validUntil = snapshotExpiryUtc.HasValue
            ? DateTime.SpecifyKind(snapshotExpiryUtc.Value, DateTimeKind.Utc)
            : new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        await WithDbContextAsync(async (db, ct) =>
        {
        var customerName = await db.IssuedLicenses.AsNoTracking()
            .Where(il => EF.Functions.ILike(il.LicenseKey, normalizedKeyUpper))
            .OrderByDescending(il => il.IssuedAtUtc)
            .Select(il => il.CustomerName)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        string? displayCustomer = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim();

        var stale = await db.ActivatedLicenses
            .Where(a => a.MachineFingerprint == machine && a.IsActive)
            .Where(a => !EF.Functions.ILike(a.LicenseKey, normalizedKeyUpper))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var row in stale)
            row.IsActive = false;

        var existing = await db.ActivatedLicenses
            .Where(a => a.MachineFingerprint == machine && EF.Functions.ILike(a.LicenseKey, normalizedKeyUpper))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        if (existing is null)
        {
            db.ActivatedLicenses.Add(new ActivatedLicense
            {
                Id = Guid.NewGuid(),
                LicenseKey = normalizedKeyUpper,
                CustomerName = displayCustomer,
                ValidUntilUtc = validUntil,
                MachineFingerprint = machine,
                ActivatedAtUtc = now,
                LastSeenAtUtc = now,
                FeaturesJson = featuresJson,
                IsActive = true,
                CreatedByUserId = clientInfo?.InitiatingUserId,
            });
        }
        else
        {
            existing.ValidUntilUtc = validUntil;
            existing.ActivatedAtUtc = now;
            existing.LastSeenAtUtc = now;
            existing.FeaturesJson = featuresJson;
            existing.IsActive = true;
            if (!string.IsNullOrWhiteSpace(customerName))
                existing.CustomerName = displayCustomer;
            if (existing.CreatedByUserId is null && clientInfo?.InitiatingUserId is { } uid)
                existing.CreatedByUserId = uid;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static string ClipDbField(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "";
        s = s.Trim();
        return s.Length <= max ? s : s[..max];
    }

    private static string? BuildUserAgentForAudit(LicenseActivationClientInfo? clientInfo)
    {
        var ua = clientInfo?.UserAgent;
        var ctx = clientInfo?.SourceAppContext;
        if (string.IsNullOrWhiteSpace(ctx))
            return string.IsNullOrWhiteSpace(ua) ? null : ClipDbField(ua, 500);
        var tag = $"[app:{ctx.Trim().ToLowerInvariant()}] ";
        var combined = string.IsNullOrWhiteSpace(ua) ? tag.TrimEnd() : tag + ua;
        return ClipDbField(combined, 500);
    }

    private async Task TryInsertActivationAttemptAsync(
        string licenseKeyForDb,
        string machineFingerprint,
        LicenseActivationAttemptStatus status,
        string? failureReason,
        LicenseActivationClientInfo? clientInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            await WithDbContextAsync(async (db, ct) =>
            {
                var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
                var uaForAudit = BuildUserAgentForAudit(clientInfo);
                db.LicenseActivationAttempts.Add(new LicenseActivationAttempt
                {
                    Id = Guid.NewGuid(),
                    LicenseKey = licenseKeyForDb,
                    MachineFingerprint = machineFingerprint,
                    ActivationStatus = status,
                    FailureReason = string.IsNullOrWhiteSpace(failureReason) ? null : ClipDbField(failureReason, 4000),
                    ClientIp = string.IsNullOrWhiteSpace(clientInfo?.ClientIp) ? null : ClipDbField(clientInfo.ClientIp, 45),
                    UserAgent = uaForAudit,
                    ActivatedAtUtc = now,
                    DeactivatedAtUtc = null,
                });
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "License: activation attempt audit insert failed. Status={Status} keyPrefix={Prefix}",
                status,
                SafePrefix(licenseKeyForDb));
        }
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
            return WithDbContext(db => db.IssuedLicenses.AsNoTracking().Any(il =>
                EF.Functions.ILike(il.LicenseKey, normalizedLicenseKeyUpper)
                && (il.IsRevoked || il.TransferredToLicenseId != null || il.IsDeleted || il.IsCancelled)));
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
        var licenseKeyPrefix = SafePrefix(expectedLicenseKey);
        var pem = _options.Value.OfflineVerificationPublicKeyPem?.Trim();
        if (string.IsNullOrEmpty(pem))
        {
            _logger.LogWarning("License JWT: verification aborted — OfflineVerificationPublicKeyPem is empty. LicenseKeyPrefix={LicenseKeyPrefix}", licenseKeyPrefix);
            error = "Public key missing.";
            return false;
        }

        var parts = jwt.Split('.');
        if (parts.Length != 3)
        {
            _logger.LogWarning(
                "License JWT: malformed JWS (expected 3 segments). LicenseKeyPrefix={LicenseKeyPrefix}, SegmentCount={SegmentCount}, JwtLength={JwtLength}",
                licenseKeyPrefix,
                parts.Length,
                jwt.Length);
            error = "JWT must have three segments.";
            return false;
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            var rsaKey = new RsaSecurityKey(rsa.ExportParameters(false));

            var opts = _options.Value;
            var validateIssuer = !string.IsNullOrWhiteSpace(opts.LicenseJwtIssuer);
            var validateAudience = !string.IsNullOrWhiteSpace(opts.LicenseJwtAudience);
            var parms = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = rsaKey,
                ValidateIssuer = validateIssuer,
                ValidIssuer = opts.LicenseJwtIssuer,
                ValidateAudience = validateAudience,
                ValidAudience = opts.LicenseJwtAudience,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
                RequireSignedTokens = true,
            };

            var handler = new JwtSecurityTokenHandler();
            handler.InboundClaimTypeMap.Clear();

            JwtSecurityToken? diagnosticToken = null;
            try
            {
                diagnosticToken = handler.ReadJwtToken(jwt);
            }
            catch (Exception parseEx)
            {
                _logger.LogWarning(parseEx, "License JWT: could not read token for diagnostics (malformed Base64/header). LicenseKeyPrefix={LicenseKeyPrefix}", licenseKeyPrefix);
            }

            var headerAlg = diagnosticToken?.Header?.Alg ?? "(unknown)";
            _logger.LogDebug(
                "License JWT: validation parameters. LicenseKeyPrefix={LicenseKeyPrefix}, HeaderAlg={HeaderAlg}, ValidateIssuer={ValidateIssuer}, ValidateAudience={ValidateAudience}, ExpectedIssuer={ExpectedIssuer}, ExpectedAudience={ExpectedAudience}, ClockSkewMinutes={ClockSkewMinutes}, RequireMachineBinding={RequireMachineBinding}",
                licenseKeyPrefix,
                headerAlg,
                validateIssuer,
                validateAudience,
                validateIssuer ? SafeLogFragment(opts.LicenseJwtIssuer) : "(skipped)",
                validateAudience ? SafeLogFragment(opts.LicenseJwtAudience) : "(skipped)",
                2,
                opts.RequireMachineBinding);

            JwtSecurityToken jwtToken;
            try
            {
                handler.ValidateToken(jwt, parms, out var validatedToken);
                jwtToken = (JwtSecurityToken)validatedToken;
            }
            catch (SecurityTokenExpiredException ex)
            {
                error = ex.Message;
                _logger.LogWarning(
                    "License JWT: lifetime validation failed — token expired (ValidateLifetime=true). LicenseKeyPrefix={LicenseKeyPrefix}, HeaderAlg={HeaderAlg}, ExpiresUtc={ExpiresUtc}, UtcNow={UtcNow}",
                    licenseKeyPrefix,
                    headerAlg,
                    ex.Expires,
                    DateTime.UtcNow);
                return false;
            }
            catch (SecurityTokenInvalidSignatureException ex)
            {
                error = ex.Message;
                _logger.LogWarning(
                    ex,
                    "License JWT: signature invalid — wrong key, wrong algorithm, or corrupted token. LicenseKeyPrefix={LicenseKeyPrefix}, HeaderAlg={HeaderAlg}, SigningKeyType=RsaSecurityKey",
                    licenseKeyPrefix,
                    headerAlg);
                return false;
            }
            catch (SecurityTokenInvalidIssuerException ex)
            {
                error = ex.Message;
                _logger.LogWarning(
                    "License JWT: issuer mismatch. LicenseKeyPrefix={LicenseKeyPrefix}, HeaderAlg={HeaderAlg}, Message={Message}",
                    licenseKeyPrefix,
                    headerAlg,
                    ex.Message);
                return false;
            }
            catch (SecurityTokenInvalidAudienceException ex)
            {
                error = ex.Message;
                _logger.LogWarning(
                    "License JWT: audience mismatch. LicenseKeyPrefix={LicenseKeyPrefix}, HeaderAlg={HeaderAlg}, Message={Message}",
                    licenseKeyPrefix,
                    headerAlg,
                    ex.Message);
                return false;
            }
            catch (SecurityTokenException ex)
            {
                error = ex.Message;
                _logger.LogWarning(
                    ex,
                    "License JWT: validation failed (SecurityTokenException). LicenseKeyPrefix={LicenseKeyPrefix}, ExceptionType={ExceptionType}, HeaderAlg={HeaderAlg}, Message={Message}",
                    licenseKeyPrefix,
                    ex.GetType().Name,
                    headerAlg,
                    ex.Message);
                return false;
            }

            _logger.LogInformation(
                "License JWT: signature and standard claims validated successfully. LicenseKeyPrefix={LicenseKeyPrefix}, HeaderAlg={HeaderAlg}, ValidFromUtc={ValidFromUtc:o}, ValidToUtc={ValidToUtc:o}, Issuer={Issuer}, Audience={Audience}",
                licenseKeyPrefix,
                jwtToken.Header?.Alg ?? headerAlg,
                jwtToken.ValidFrom,
                jwtToken.ValidTo,
                SafeLogFragment(jwtToken.Issuer),
                SafeLogFragment(jwtToken.Audiences?.FirstOrDefault()));

            var licenseKeyClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "licenseKey")?.Value;
            if (string.IsNullOrEmpty(licenseKeyClaim))
            {
                _logger.LogWarning("License JWT: required claim missing. LicenseKeyPrefix={LicenseKeyPrefix}, Claim=licenseKey", licenseKeyPrefix);
                error = "licenseKey claim missing.";
                return false;
            }

            if (!string.Equals(licenseKeyClaim, expectedLicenseKey, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "License JWT: licenseKey claim does not match request key. LicenseKeyPrefix={LicenseKeyPrefix}, ClaimKeyPrefix={ClaimKeyPrefix}",
                    licenseKeyPrefix,
                    SafePrefix(licenseKeyClaim.Trim().ToUpperInvariant()));
                error = "licenseKey mismatch.";
                return false;
            }

            var boundHash = jwtToken.Claims.FirstOrDefault(c => c.Type == "machineHash")?.Value;
            var licenseIsFloating = string.IsNullOrEmpty(boundHash)
                || string.Equals(boundHash, "FLOATING", StringComparison.OrdinalIgnoreCase);

            if (!licenseIsFloating)
            {
                var boundFp = boundHash!.Trim();
                var requireBinding = _options.Value.RequireMachineBinding;
                var localFp = _storage.MachineHashHex;
                var match = string.Equals(boundFp, localFp, StringComparison.OrdinalIgnoreCase);
                if (!requireBinding)
                {
                    _logger.LogWarning(
                        "License JWT: machineHash present but binding bypassed (RequireMachineBinding=false). LicenseKeyPrefix={LicenseKeyPrefix}, JwtMachineHashPrefix={JwtMachineHashPrefix}, LocalMachineHashPrefix={LocalMachineHashPrefix}, FingerprintMatch={FingerprintMatch}",
                        licenseKeyPrefix,
                        SafePrefix(boundFp),
                        SafePrefix(localFp),
                        match);
                }
                else if (!match)
                {
                    _logger.LogWarning(
                        "License JWT: machine fingerprint mismatch (machine-bound license). LicenseKeyPrefix={LicenseKeyPrefix}, JwtMachineHashPrefix={JwtMachineHashPrefix}, LocalMachineHashPrefix={LocalMachineHashPrefix}, FingerprintMatch={FingerprintMatch}",
                        licenseKeyPrefix,
                        SafePrefix(boundFp),
                        SafePrefix(localFp),
                        false);
                    error = "Machine hash mismatch.";
                    return false;
                }
                else
                {
                    _logger.LogInformation(
                        "License JWT: machine fingerprint matched (machine-bound license). LicenseKeyPrefix={LicenseKeyPrefix}, JwtMachineHashPrefix={JwtMachineHashPrefix}, LocalMachineHashPrefix={LocalMachineHashPrefix}, FingerprintMatch={FingerprintMatch}",
                        licenseKeyPrefix,
                        SafePrefix(boundFp),
                        SafePrefix(localFp),
                        true);
                }
            }
            else
            {
                _logger.LogInformation(
                    "License JWT: floating license (no machine binding in token). LicenseKeyPrefix={LicenseKeyPrefix}, MachineBindingMode={MachineBindingMode}",
                    licenseKeyPrefix,
                    "FLOATING");
            }

            _logger.LogInformation(
                "License JWT: full offline verification succeeded. LicenseKeyPrefix={LicenseKeyPrefix}, NotExpired={NotExpired}, MachineBindingOutcome={MachineBindingOutcome}",
                licenseKeyPrefix,
                jwtToken.ValidTo >= DateTime.UtcNow,
                licenseIsFloating ? "FLOATING" : (_options.Value.RequireMachineBinding ? "BOUND_MATCHED" : "BOUND_BYPASS"));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "License JWT: unexpected error during verification. LicenseKeyPrefix={LicenseKeyPrefix}", licenseKeyPrefix);
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

    /// <summary>Shortens issuer/audience strings for structured logs (no secrets).</summary>
    private static string SafeLogFragment(string? value, int maxLen = 72)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "(none)";
        var t = value.Trim();
        return t.Length <= maxLen ? t : t[..maxLen] + "…";
    }

    private sealed record RemoteLicenseRequest(string MachineHash, string LicenseKey);

    private sealed record RemoteLicenseResponseDto(bool Valid);
}
