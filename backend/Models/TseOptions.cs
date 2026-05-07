namespace KasseAPI_Final.Models
{
    /// <summary>
    /// TSE (Technische Sicherheitseinrichtung) konfigürasyonu.
    /// TseMode: Off | Demo | Device (payment / QR policy).
    /// Mode: Fake | Real (closing-signing backend via <see cref="KasseAPI_Final.Tse.ITseProvider"/>).
    /// </summary>
    public class TseOptions
    {
        public const string SectionName = "Tse";

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
    }
}
