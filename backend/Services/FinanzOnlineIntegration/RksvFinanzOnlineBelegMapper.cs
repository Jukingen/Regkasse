using KasseAPI_Final.Rksv;
using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Maps RKSV QR wire / compact JWS to the BMF <c>belegpruefung.beleg</c> DEP machine-code string.
/// Full QR (<c>{machineCode}_{jws}</c>) is not a valid beleg candidate by itself.
/// </summary>
public static class RksvFinanzOnlineBelegMapper
{
    public const string BelegInvalidErrorCode = "RKS_BELEG_INVALID";

    /// <summary>
    /// Resolves a DEP-pattern machine code suitable for <see cref="FinanzOnlineRkdbBelegpruefungCommand.Beleg"/>.
    /// Prefers JWS / QR extraction before treating the raw string as a DEP line (avoids QR-wire false positives).
    /// </summary>
    public static bool TryResolveBeleg(string? qrPayloadOrJwsOrBeleg, out string beleg, out string? errorMessage)
    {
        beleg = string.Empty;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(qrPayloadOrJwsOrBeleg))
        {
            errorMessage = "QR / machine-code payload is empty.";
            return false;
        }

        var input = qrPayloadOrJwsOrBeleg.Trim();

        // Compact JWS first (three Base64URL segments).
        if (SignaturePipeline.TryGetMachineCodeFromCompactJws(input, out var fromJws)
            && FinanzOnlineRkdbBelegpruefungValidator.IsValidDepCandidate(fromJws))
        {
            beleg = fromJws;
            return true;
        }

        // Standard / legacy QR wire → machine code from embedded JWS.
        var parsed = RksvQrParser.Parse(input);
        if (parsed.Success && parsed.Payload is { } qr
            && SignaturePipeline.TryGetMachineCodeFromCompactJws(qr.Signature, out var fromQrJws)
            && FinanzOnlineRkdbBelegpruefungValidator.IsValidDepCandidate(fromQrJws))
        {
            beleg = fromQrJws;
            return true;
        }

        if (TryExtractTrailingCompactJws(input, out var trailingJws)
            && SignaturePipeline.TryGetMachineCodeFromCompactJws(trailingJws, out var fromTrailing)
            && FinanzOnlineRkdbBelegpruefungValidator.IsValidDepCandidate(fromTrailing))
        {
            beleg = fromTrailing;
            return true;
        }

        // Already a DEP machine-code line (no JWS dots).
        if (FinanzOnlineRkdbBelegpruefungValidator.IsValidDepCandidate(input))
        {
            beleg = input;
            return true;
        }

        errorMessage =
            "Unable to derive a BMF DEP machine-code beleg from QR/JWS payload. " +
            "Expected a valid (_segment){12,13} string, compact JWS, or RKSV QR wire format.";
        return false;
    }

    /// <summary>
    /// Finds a trailing compact JWS after the last underscore that yields three Base64URL segments.
    /// </summary>
    internal static bool TryExtractTrailingCompactJws(string qrWire, out string compactJws)
    {
        compactJws = string.Empty;
        if (string.IsNullOrWhiteSpace(qrWire))
            return false;

        var trimmed = qrWire.Trim();
        for (var i = trimmed.Length - 1; i >= 0; i--)
        {
            if (trimmed[i] != '_')
                continue;

            var candidate = trimmed[(i + 1)..];
            var parts = candidate.Split('.');
            if (parts.Length != 3)
                continue;
            if (parts.Any(p => string.IsNullOrEmpty(p) || p.Contains('=', StringComparison.Ordinal)))
                continue;

            compactJws = candidate;
            return true;
        }

        return false;
    }
}
