namespace KasseAPI_Final.Configuration;

/// <summary>
/// On-premise license validation (offline JWS and/or optional remote server).
/// </summary>
public sealed class LicenseOptions
{
    public const string SectionName = "License";

    /// <summary>
    /// When <c>false</c>, mandant/deployment license enforcement (middleware, payments, login lockdown) is skipped.
    /// Intended for Development hosts; production should keep <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>RSA public key (PEM) used to verify offline activation JWT (RS256).</summary>
    public string? OfflineVerificationPublicKeyPem { get; set; }

    /// <summary>
    /// RSA private key (PEM, PKCS#1 or PKCS#8) used to issue (sign) new license JWTs from the admin panel.
    /// Leave <c>null</c> on customer POS deployments — the generation endpoint then returns 503 (Service Unavailable).
    /// Only the central license-issuance server should carry this key. Treat as a high-value secret:
    /// store via <c>dotnet user-secrets</c>, environment variable, or a secret manager — never in source-controlled files.
    /// For local development only, you may set this in <c>appsettings.Development.json</c> (this repo gitignores that file) together with <see cref="OfflineVerificationPublicKeyPem"/> from the same RSA key pair so issued JWTs verify on the same host.
    /// </summary>
    public string? SigningPrivateKeyPem { get; set; }

    /// <summary>
    /// When <c>true</c>, allows activating with only a formatted <c>REGK-</c> license key when neither
    /// <see cref="OfflineVerificationPublicKeyPem"/> (JWT) nor <see cref="RemoteValidationUrl"/> is used and the key
    /// is not present in <c>issued_licenses</c> on this host. Intended for small on-prem deployments; keep
    /// <c>false</c> in hardened production. Development hosting always permits this path regardless of this flag.
    /// </summary>
    public bool AllowKeyOnlyOfflineActivation { get; set; }

    /// <summary>Optional HTTPS endpoint for online activation/validation (POST JSON body).</summary>
    public string? RemoteValidationUrl { get; set; }

    /// <summary>Optional API key sent as <c>X-Api-Key</c> for remote validation.</summary>
    public string? RemoteValidationApiKey { get; set; }

    /// <summary>
    /// Controls whether the offline JWT's <c>machineHash</c> claim must match the local machine.
    /// <list type="bullet">
    /// <item><c>true</c> (code default for safety): enforce binding — if the license JWT carries a non-empty,
    /// non-<c>FLOATING</c> <c>machineHash</c>, it must equal the local fingerprint or activation/validation fails.</item>
    /// <item><c>false</c>: skip the <c>machineHash</c> check entirely. The license becomes portable across servers
    /// (e.g., a customer moving to new hardware can re-activate without re-issuing). Floating licenses
    /// (issued without a machine claim) are accepted under both modes.</item>
    /// </list>
    /// Disabling this weakens anti-portability guarantees; use only for legitimate migrations.
    /// </summary>
    public bool RequireMachineBinding { get; set; } = true;

    /// <summary>
    /// Calendar-day thresholds for mandant expiry emails and in-app reminders
    /// (exact match on <c>daysRemaining</c>). Default: 30 / 14 / 7 / 1 days before expiry.
    /// </summary>
    public int[] ReminderDays { get; set; } = [30, 14, 7, 1];

    /// <summary>
    /// When true, also send one overdue/expired email per license validity date
    /// (deduped via billing audit; limited to tenants within <see cref="ArchiveAfterDays"/> overdue).
    /// </summary>
    public bool SendExpiredReminder { get; set; } = true;

    /// <summary>
    /// When true, send dedicated grace-period emails while the mandant is inside
    /// <see cref="GracePeriodDays"/> after expiry (see <see cref="GraceReminderDays"/>).
    /// </summary>
    public bool SendGracePeriodReminders { get; set; } = true;

    /// <summary>
    /// Remaining-grace-day anchors for grace reminder emails (exact match).
    /// Default: 6 / 4 / 2 days remaining in the 7-day grace window.
    /// </summary>
    public int[] GraceReminderDays { get; set; } = [6, 4, 2];

    /// <summary>
    /// When true (and <see cref="SendGracePeriodReminders"/> is true), also send when
    /// remaining grace days are ≤ <see cref="GraceUrgentReminderDays"/>.
    /// </summary>
    public bool SendGraceUrgentReminder { get; set; } = true;

    /// <summary>Inclusive upper bound of remaining grace days for the urgent daily reminder.</summary>
    public int GraceUrgentReminderDays { get; set; } = 1;

    /// <summary>Deep link to the FA license renewal page included in reminder emails.</summary>
    public string AdminLicenseUrl { get; set; } = "https://admin.regkasse.at/license";

    /// <summary>Deep link to the FA dashboard included in renewal confirmation emails.</summary>
    public string AdminDashboardUrl { get; set; } = "https://admin.regkasse.at/dashboard";

    /// <summary>UTC hour (0–23) for the daily reminder tick.</summary>
    public int ReminderCheckHourUtc { get; set; } = 9;

    /// <summary>UTC minute (0–59) for the daily reminder tick.</summary>
    public int ReminderCheckMinuteUtc { get; set; } = 0;

    /// <summary>Optional JWT issuer claim validation for offline activation JWTs.</summary>
    public string? LicenseJwtIssuer { get; set; }

    /// <summary>Optional JWT audience claim validation for offline activation JWTs.</summary>
    public string? LicenseJwtAudience { get; set; }

    /// <summary>POS feature flag surfaced by <c>GET /api/license/features</c>.</summary>
    public bool LicenseFeatureAllowOffline { get; set; } = true;

    /// <summary>Maximum cashiers for POS; <c>-1</c> means unlimited.</summary>
    public int LicenseFeatureMaxCashiers { get; set; } = -1;

    /// <summary>
    /// Mandant grace period length in days after <c>license_valid_until_utc</c>.
    /// Must stay at the production policy value (7). Do not use large Development sentinels
    /// (e.g. 999) — remaining grace is <c>GracePeriodDays - daysOverdue</c> and would show as ~997 Tage.
    /// </summary>
    public int GracePeriodDays { get; set; } = LicenseGracePeriodConfig.DefaultGracePeriodDays;

    /// <summary>Days before mandant expiry when pre-expiry warnings begin.</summary>
    public int WarningDaysBeforeExpiry { get; set; } = LicenseGracePeriodConfig.DefaultWarningDaysBeforeExpiry;

    /// <summary>Additional days after grace before lockdown; zero blocks immediately when grace ends.</summary>
    public int BlockAfterGraceDays { get; set; } = LicenseGracePeriodConfig.DefaultBlockAfterGraceDays;

    /// <summary>
    /// Days overdue after which Locked becomes Archived (POS blocked, FA read-only).
    /// Must be greater than <see cref="GracePeriodDays"/>.
    /// </summary>
    public int ArchiveAfterDays { get; set; } = LicenseGracePeriodConfig.DefaultArchiveAfterDays;
}
