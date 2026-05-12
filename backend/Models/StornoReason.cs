namespace KasseAPI_Final.Models;

/// <summary>
/// RKSV-oriented classification for full receipt cancellation (Storno), distinct from partial refund.
/// </summary>
public enum StornoReason
{
    /// <summary>Sentinel only — must not be persisted; use a concrete RKSV reason.</summary>
    None = -1,
    FalscherBetrag = 0,
    KundeStorniert = 1,
    TechnischerFehler = 2,
    Anderes = 3
}
