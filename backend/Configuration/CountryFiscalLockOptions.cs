namespace KasseAPI_Final.Configuration;

/// <summary>
/// Composite bind target for DE/CH fiscal startup lock (<c>ValidateOnStart</c>).
/// Austrian RKSV/TSE lock stays on <c>TseOptions</c> / <c>TseProductionOptionsValidator</c>.
/// </summary>
public sealed class CountryFiscalLockOptions
{
    public KassenSicherheitOptions KassenSicherheit { get; set; } = new();

    public MwstOptions Mwst { get; set; } = new();

    public QrRechnungOptions QrRechnung { get; set; } = new();
}
