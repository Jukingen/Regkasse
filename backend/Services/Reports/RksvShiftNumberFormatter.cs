namespace KasseAPI_Final.Services.Reports;

/// <summary>Formats cashier shift identity for RKSV report display (Schicht-Nr.).</summary>
public static class RksvShiftNumberFormatter
{
    public static string? Format(Guid? shiftId)
    {
        if (!shiftId.HasValue || shiftId.Value == Guid.Empty)
            return null;

        return shiftId.Value.ToString("N")[..8].ToUpperInvariant();
    }

    public static string FormatOrDash(Guid? shiftId) =>
        Format(shiftId) ?? "—";

    public static string? Format(int? shiftNumber) =>
        shiftNumber is > 0
            ? shiftNumber.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;

    public static string FormatOrDash(int? shiftNumber) =>
        Format(shiftNumber) ?? "—";

    public static string FormatOrDash(string? shiftNumber) =>
        string.IsNullOrWhiteSpace(shiftNumber) ? "—" : shiftNumber.Trim();
}
