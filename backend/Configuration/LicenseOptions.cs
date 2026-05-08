namespace KasseAPI_Final.Configuration;

/// <summary>
/// On-premise license validation (offline JWS and/or optional remote server).
/// </summary>
public sealed class LicenseOptions
{
    public const string SectionName = "License";

    /// <summary>RSA public key (PEM) used to verify offline activation JWT (RS256).</summary>
    public string? OfflineVerificationPublicKeyPem { get; set; }

    /// <summary>
    /// RSA private key (PEM, PKCS#1 or PKCS#8) used to issue (sign) new license JWTs from the admin panel.
    /// Leave <c>null</c> on customer POS deployments — the generation endpoint then returns 503 (Service Unavailable).
    /// Only the central license-issuance server should carry this key. Treat as a high-value secret:
    /// store via <c>dotnet user-secrets</c>, environment variable, or a secret manager — never in source-controlled files.
    /// </summary>
    public string? SigningPrivateKeyPem { get; set; }

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
    /// Calendar-day thresholds evaluated daily by the license reminder hosted service for in-app reminders
    /// (matches <c>daysRemaining</c> exactly) plus the implicit <c>&lt;= 15</c> day rule in that service.
    /// </summary>
    public int[] ReminderDays { get; set; } = [30, 15, 7, 3, 1];

    /// <summary>UTC hour (0–23) for the daily reminder tick.</summary>
    public int ReminderCheckHourUtc { get; set; } = 9;

    /// <summary>UTC minute (0–59) for the daily reminder tick.</summary>
    public int ReminderCheckMinuteUtc { get; set; } = 0;
}
