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

        // --- SLA monitoring (operational targets; not a fiscal certificate) ---

        /// <summary>Target TSE probe availability (%) for SLA uptime (Healthy + Degraded samples).</summary>
        public double SlaTargetUptimePercent { get; set; } = 99.5;

        /// <summary>Target average probe response time (ms).</summary>
        public int SlaTargetResponseTimeMs { get; set; } = 2000;

        /// <summary>Target signed fiscal receipt success rate (%).</summary>
        public double SlaTargetSuccessRatePercent { get; set; } = 99.0;

        /// <summary>Lookback hours for <c>ITseSlaMonitorService.GetCurrentSlaStatusAsync</c>.</summary>
        public int SlaStatusLookbackHours { get; set; } = 24;

        // --- Capacity planning (receipt volume vs configured device limits) ---

        /// <summary>Days of receipt history for capacity trends (clamped 7–90).</summary>
        public int CapacityLookbackDays { get; set; } = 30;

        /// <summary>Indicative max signed receipts per active signing device per day.</summary>
        public int CapacityPerDevicePerDay { get; set; } = 5000;

        /// <summary>Indicative max signed receipts per active signing device per UTC hour.</summary>
        public int CapacityPerDevicePerHour { get; set; } = 400;

        /// <summary>Utilization (%) that raises a capacity warning.</summary>
        public double CapacityWarningUtilizationPercent { get; set; } = 80;

        /// <summary>Utilization (%) that raises a critical capacity alert.</summary>
        public double CapacityCriticalUtilizationPercent { get; set; } = 95;

        /// <summary>Days-ahead window for capacity-reach date alerts.</summary>
        public int CapacityReachAlertDays { get; set; } = 60;

        // --- Disaster recovery runbooks (simulation drills; not live failover) ---

        /// <summary>Target RTO (minutes) shown on DR status / drill reports.</summary>
        public int DrRtoTargetMinutes { get; set; } = 30;

        /// <summary>Minimum healthy idle backup devices required for DR Ready.</summary>
        public int DrMinHealthyBackups { get; set; } = 1;

        /// <summary>Max age (days) of last successful drill before readiness messaging warns.</summary>
        public int DrMaxDrillAgeDays { get; set; } = 90;

        // --- Indicative cost monitoring (EUR; not billing / invoices) ---

        /// <summary>Estimated EUR cost per signed fiscal receipt (cloud API / SCU usage).</summary>
        public decimal CostPerSignedTransactionEur { get; set; } = 0.002m;

        /// <summary>Estimated monthly EUR fee per active primary (or failover-active) TSE device.</summary>
        public decimal CostMonthlyPrimaryDeviceEur { get; set; } = 15m;

        /// <summary>Estimated monthly EUR fee per idle active backup TSE device.</summary>
        public decimal CostMonthlyBackupDeviceEur { get; set; } = 5m;

        /// <summary>Estimated EUR overhead attributed to each successful failover event.</summary>
        public decimal CostPerFailoverEventEur { get; set; } = 2m;

        /// <summary>Period-over-period cost increase (%) that raises a warning anomaly.</summary>
        public decimal CostAnomalyWarningIncreasePercent { get; set; } = 40m;

        /// <summary>Period-over-period cost increase (%) that raises a critical anomaly.</summary>
        public decimal CostAnomalyCriticalIncreasePercent { get; set; } = 100m;

        /// <summary>Daily cost vs period-day average multiplier treated as an in-period spike.</summary>
        public double CostDailySpikeMultiplier { get; set; } = 2.5;

        /// <summary>Signed tx/day per primary below which consolidation is recommended.</summary>
        public double CostLowUtilizationDailyTxThreshold { get; set; } = 20;

        /// <summary>Successful failovers in the lookback window that trigger a churn recommendation.</summary>
        public int CostHighFailoverCountThreshold { get; set; } = 3;

        // --- Indicative sustainability / green IT (not certified LCA / ESG audit) ---

        /// <summary>Estimated kg CO₂e per signed fiscal transaction (cloud API call footprint).</summary>
        public double SustainabilityKgCo2PerSignedTransaction { get; set; } = 0.0008;

        /// <summary>Estimated kWh/day for an active cloud/hardware TSE device.</summary>
        public double SustainabilityKwhPerCloudDeviceDay { get; set; } = 0.12;

        /// <summary>Estimated kWh/day for Soft/Fake TSE (much lower than cloud/hardware).</summary>
        public double SustainabilityKwhPerSoftDeviceDay { get; set; } = 0.01;

        /// <summary>Grid intensity kg CO₂e per kWh used to convert energy → carbon.</summary>
        public double SustainabilityKgCo2PerKwh { get; set; } = 0.23;

        /// <summary>EUR saved per kWh avoided (indicative electricity rate).</summary>
        public decimal SustainabilityEurPerKwh { get; set; } = 0.28m;

        /// <summary>Industry-average kg CO₂e per signed transaction for percentile comparison.</summary>
        public double SustainabilityIndustryKgCo2PerTransaction { get; set; } = 0.0025;

        /// <summary>Default lookback days for sustainability report when period not specified.</summary>
        public int SustainabilityDefaultLookbackDays { get; set; } = 30;

        // --- Predictive health analytics (heuristic; not a certified ML model) ---

        /// <summary>Days of health samples used for trend / risk scoring.</summary>
        public int PredictiveLookbackDays { get; set; } = 14;

        /// <summary>Health-score points/day decline that raises a trend risk factor.</summary>
        public double PredictiveDeclinePerDayWarning { get; set; } = 1.5;

        /// <summary>Failover events involving a device that raise a risk factor.</summary>
        public int PredictiveFailoverCountWarning { get; set; } = 2;

        /// <summary>Failure probability (%) mapped to Medium risk.</summary>
        public double PredictiveMediumProbability { get; set; } = 30;

        /// <summary>Failure probability (%) mapped to High risk.</summary>
        public double PredictiveHighProbability { get; set; } = 55;

        /// <summary>Failure probability (%) mapped to Critical risk.</summary>
        public double PredictiveCriticalProbability { get; set; } = 75;

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
