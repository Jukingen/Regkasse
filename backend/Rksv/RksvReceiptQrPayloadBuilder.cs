using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Rksv;

/// <summary>
/// Builds the RKSV receipt QR that BMF CheckSingleReceipt accepts: §9 machine code plus a standard-Base64 Sig-Wert.
/// </summary>
public static class RksvReceiptQrPayloadBuilder
{
    /// <summary>
    /// Combines the RKSV §9 machine code embedded in the JWS payload with the JWS signature as standard Base64.
    /// Wire layout: <c>{machineCode}_{Sig-Wert}</c> — 13 underscore groups, last field has no <c>_</c>
    /// (BMF <c>BASICCONSTRAINTS_NUMBER_OF_ELEMENTS</c>). Compact JWS is not appended: Base64URL payload/signature
    /// segments use <c>_</c> and would split into more than 13 fields.
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

        byte[] signatureBytes;
        try
        {
            signatureBytes = TseCryptoHelper.FromBase64UrlNoPadding(parts[2]);
        }
        catch (TsePipelineException)
        {
            return false;
        }

        if (signatureBytes.Length == 0)
            return false;

        qrPayload = $"{machineCode}_{Convert.ToBase64String(signatureBytes)}";
        return true;
    }

    public static string? BuildFromCompactJwsOrNull(string? compactJws) =>
        TryBuildFromCompactJws(compactJws, out var qr) ? qr : null;
}
