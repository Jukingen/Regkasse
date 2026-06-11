using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Rksv;

/// <summary>
/// Builds RKSV receipt QR wire format: BMF §9 machine code (from signed JWS payload) + compact JWS.
/// </summary>
public static class RksvReceiptQrPayloadBuilder
{
    /// <summary>
    /// Combines the RKSV §9 machine code embedded in the JWS payload with the full compact JWS.
    /// Wire layout: <c>{machineCode}_{header.payload.signature}</c> (11 body fields + JWS per <see cref="RksvQrPayloadLayout.StandardRksvV1"/>).
    /// </summary>
    public static bool TryBuildFromCompactJws(string? compactJws, out string qrPayload)
    {
        qrPayload = string.Empty;
        if (string.IsNullOrWhiteSpace(compactJws))
            return false;

        var trimmed = compactJws.Trim();
        var parts = trimmed.Split('.');
        if (parts.Length != 3)
            return false;

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part) || part.Contains('='))
                return false;
        }

        if (!SignaturePipeline.TryGetMachineCodeFromCompactJws(trimmed, out var machineCode))
            return false;

        qrPayload = $"{machineCode}_{trimmed}";
        return true;
    }

    public static string? BuildFromCompactJwsOrNull(string? compactJws) =>
        TryBuildFromCompactJws(compactJws, out var qr) ? qr : null;
}
