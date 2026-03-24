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
    }
}
