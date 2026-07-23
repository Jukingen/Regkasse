using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// TSE (Technische Sicherheitseinrichtung) konfigürasyonu.
    /// TseMode: Off | Demo | Device (payment / QR policy).
    /// Mode: Fake | Real (closing-signing backend via <see cref="KasseAPI_Final.Tse.ITseProvider"/>).
    /// Provider: active cloud/hardware vendor name (fiskaly | epson | swissbit) when Mode=Real.
    /// </summary>
    public class TseOptions
    {
        public const string SectionName = "Tse";

        public const string ProviderFiskaly = "fiskaly";
        public const string ProviderEpson = "epson";
        public const string ProviderSwissbit = "swissbit";
        public const string ProviderFake = "fake";
        public const string ProviderSoft = "soft";

        /// <summary>
        /// Closing signatures: Fake = simulated JWS without hardware; Real = RKSV pipeline + device readiness.
        /// </summary>
        public string Mode { get; set; } = "Real";

        public bool IsFakeSigningMode => string.Equals(Mode, "Fake", System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// TSE modu: tek kaynak.
        /// Off: TSE kapalı, tseRequired yok sayılır, sadece NON_FISCAL QR.
        /// Demo: Cihaz yoksa Soft TSE, signature chain DB'den devam eder.
        /// Device: Gerçek TSE cihazı zorunlu.
        /// </summary>
        public string TseMode { get; set; } = "Device";

        /// <summary>Soft TSE kullanılsın mı (TseMode=Demo iken).</summary>
        public bool UseSoftTseWhenNoDevice => string.Equals(TseMode, "Demo", StringComparison.OrdinalIgnoreCase);

        /// <summary>TSE tamamen kapalı mı.</summary>
        public bool IsOff => string.Equals(TseMode, "Off", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Deployment label for vendor APIs (e.g. Production / Test / Sandbox). Informational + Fiskaly base-url hints.
        /// </summary>
        [MaxLength(32)]
        public string Environment { get; set; } = "Production";

        /// <summary>
        /// Development safety valve: when true, daily closing may continue without a connected device,
        /// but only when the active provider is fake/simulated.
        /// </summary>
        public bool AllowSimulatedDailyClosing { get; set; } = false;

        /// <summary>Background probe interval for hardware TSE readiness (seconds).</summary>
        public int HealthCheckIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// When true and cached health is Offline, eligible cash/card payments are accepted as server-side offline intents.
        /// </summary>
        public bool OfflineModeEnabled { get; set; } = true;

        /// <summary>Cap of NonFiscalPending rows per cash register (server-side offline queue).</summary>
        public int MaxOfflineTransactionsPerCashRegister { get; set; } = 50;

        /// <summary>Background replay worker cadence for NonFiscalPending intents (seconds).</summary>
        public int AutoReplayIntervalSeconds { get; set; } = 60;

        /// <summary>Consecutive probe failures before status becomes Offline.</summary>
        public int OfflineAfterConsecutiveFailures { get; set; } = 3;

        /// <summary>Minimum consecutive failures before leaving Online (first step toward Degraded/Offline ladder).</summary>
        public int DegradedAfterConsecutiveFailures { get; set; } = 1;

        /// <summary>
        /// When true (default), tenant onboarding / cash-register create auto-provisions a TSE device row
        /// and signature-chain state via <c>ITseProvisioningService</c>. Ignored when <see cref="IsOff"/>.
        /// </summary>
        public bool AutoProvisionOnTenantCreate { get; set; } = true;

        /// <summary>
        /// When true (default), <c>ITseFailoverService.CheckAndFailoverAsync</c> may activate a healthy backup.
        /// </summary>
        public bool AutoFailoverEnabled { get; set; } = true;

        /// <summary>Minimum health score (0–100) classified as <c>Healthy</c> for device failover policy.</summary>
        public int FailoverHealthyMinScore { get; set; } = 80;

        /// <summary>Minimum health score (0–100) classified as <c>Degraded</c> (below this → Unhealthy/Offline).</summary>
        public int FailoverDegradedMinScore { get; set; } = 50;

        /// <summary>Minimum seconds between health samples for the same device unless score/status changes.</summary>
        public int HealthSampleMinIntervalSeconds { get; set; } = 300;

        /// <summary>Days to retain <c>tse_device_health_samples</c> rows (clamped 7–90 at write time).</summary>
        public int HealthSampleRetentionDays { get; set; } = 30;

        /// <summary>Days before ExpiresAt when certificate is treated as ExpiringSoon (default 30).</summary>
        public int CertificateExpiringSoonDays { get; set; } = 30;

        /// <summary>Probe response time (ms) treated as slow / warning (aligns with POS SLOW_MS ≈ 3000).</summary>
        public int HealthSlowResponseMs { get; set; } = 3000;

        /// <summary>Probe response time (ms) treated as critically slow.</summary>
        public int HealthCriticalResponseMs { get; set; } = 10000;

        /// <summary>Failed probe rate (%) that raises a performance warning.</summary>
        public double HealthErrorRateWarningPercent { get; set; } = 20;

        /// <summary>Failed probe rate (%) that raises a critical performance alert.</summary>
        public double HealthErrorRateCriticalPercent { get; set; } = 50;

        /// <summary>Lookback hours for <c>ITsePerformanceService.CheckPerformanceAnomaliesAsync</c>.</summary>
        public int HealthPerformanceLookbackHours { get; set; } = 24;

        /// <summary>
        /// Optional extra recipients for TSE failover alert emails (comma/semicolon-separated).
        /// Super Admin Identity users with confirmed email are always included when present.
        /// </summary>
        [MaxLength(1000)]
        public string? FailoverAlertEmails { get; set; }

        /// <summary>
        /// Active vendor for Real mode: <c>fiskaly</c> (supported), <c>epson</c> / <c>swissbit</c> (stubs).
        /// When empty, inferred from Mode / Fiskaly config / Soft-Demo.
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>Named vendor connection blocks (<c>Tse:Providers:fiskaly</c>, …).</summary>
        public Dictionary<string, TseVendorConnectionOptions> Providers { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        public TseVendorConnectionOptions? GetVendorConnection(string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName) || Providers.Count == 0)
                return null;

            return Providers.TryGetValue(providerName.Trim(), out var conn) ? conn : null;
        }

        /// <summary>Normalizes a provider label for dictionary / factory lookups.</summary>
        public static string NormalizeProviderName(string? providerName) =>
            string.IsNullOrWhiteSpace(providerName)
                ? string.Empty
                : providerName.Trim().ToLowerInvariant();
    }
}
