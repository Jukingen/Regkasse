namespace KasseAPI_Final.Models
{
    /// <summary>
    /// RKSV uyumlu vergi tipleri - API contract için string enum standardı.
    /// DB'de int olarak saklanır (1=Standard, 2=Reduced, 3=Special, 4=ZeroRate).
    /// Österreich 2026 Reform: ZeroRate = 0% VAT (technisch 0% MwSt., nicht "Exempt").
    /// </summary>
    public enum TaxType
    {
        Standard = 1,
        Reduced = 2,
        Special = 3,
        /// <summary>0% VAT – Österreich 2026 Reform. (Exempt deprecated, use ZeroRate.)</summary>
        ZeroRate = 4
    }
}
