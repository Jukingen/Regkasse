namespace KasseAPI_Final.Services.Lodging;

/// <summary>
/// Display name for the single cart line that settles an open folio.
/// The line is priced from the folio balance on the server, not from a client amount.
/// </summary>
public static class LodgingStayLine
{
    public const int CheckoutTableNumber = 1;

    public static string Format(string roomNumber) => $"Zimmer {roomNumber} — Aufenthalt";

    public static bool IsStayLabel(string? notes) =>
        !string.IsNullOrWhiteSpace(notes)
        && notes.StartsWith("Zimmer ", StringComparison.Ordinal)
        && notes.EndsWith(" — Aufenthalt", StringComparison.Ordinal);
}
