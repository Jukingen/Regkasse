namespace KasseAPI_Final.Models
{
    /// <summary>
    /// TSE (Technische Sicherheitseinrichtung) konfigürasyonu.
    /// Tek kaynak: TseMode = Off | Demo | Device
    /// </summary>
    public class TseOptions
    {
        public const string SectionName = "Tse";

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
