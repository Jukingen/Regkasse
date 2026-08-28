using System.Text;

namespace KasseAPI_Final.Tse;

/// <summary>
/// How a stored TSE signature is represented on the wire / in <c>TseSignature</c>.
/// </summary>
public enum JwsWireFormat
{
    /// <summary>Compact JWS: <c>header.payload.signature</c> (Base64URL, no padding).</summary>
    CompactJws = 0,

    /// <summary>Local QR: <c>{machineCode}_{header.payload.signature}</c>.</summary>
    RksvQrWithCompactJws = 1,

    /// <summary>Fiskaly SIGN AT <c>qr_code_data</c>: RKSV machine code with trailing Sig-Wert.</summary>
    FiskalyMachineCode = 2
}

/// <summary>Result of normalizing a stored signature into compact JWS.</summary>
public sealed record JwsParseResult(
    bool Success,
    string CompactJws,
    JwsWireFormat? Format,
    string? Error)
{
    public static JwsParseResult Fail(string error) =>
        new(false, string.Empty, null, error);

    public static JwsParseResult Ok(string compactJws, JwsWireFormat format) =>
        new(true, compactJws, format, null);
}

/// <summary>
/// Parses compact JWS, RKSV QR wire, or Fiskaly SIGN AT machine-code QR into
/// <c>header.payload.signature</c> (Base64URL, no padding).
/// </summary>
public static class JwsParser
{
    /// <summary>BMF RKSV JWS protected header — <c>{"alg":"ES256"}</c> only (no <c>typ</c> claim).</summary>
    public static readonly byte[] RksvJwsHeaderUtf8 = "{\"alg\":\"ES256\"}"u8.ToArray();

    /// <summary>R1-ATx + 11 RKSV §9 body fields (signing input), excluding Sig-Wert.</summary>
    public const int FiskalySigningSegmentCount = 12;

    public static bool TryParse(string? input, out JwsParseResult result)
    {
        result = Parse(input);
        return result.Success;
    }

    public static JwsParseResult Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return JwsParseResult.Fail("Empty input");

        var trimmed = input.Trim();

        // Compact JWS never starts with _R1-AT (header is Base64URL JSON). RKSV QR / Fiskaly
        // machine code often has exactly two dots (header.payload.signature appended), so it
        // must be classified before the 3-part compact-JWS heuristic.
        if (trimmed.StartsWith("_R1-AT", StringComparison.Ordinal))
        {
            if (TryExtractTrailingCompactJws(trimmed, out var fromQr))
                return JwsParseResult.Ok(fromQr, JwsWireFormat.RksvQrWithCompactJws);

            if (TryReconstructFromFiskalyMachineCode(trimmed, out var reconstructed, out var reconstructError))
                return JwsParseResult.Ok(reconstructed, JwsWireFormat.FiskalyMachineCode);

            return JwsParseResult.Fail(
                reconstructError
                ?? "Stored value is an RKSV machine-code QR, not compact JWS (header.payload.signature).");
        }

        if (TryParseCompactJws(trimmed, requireJsonHeader: false, out var compact, out _))
            return JwsParseResult.Ok(compact, JwsWireFormat.CompactJws);

        var dotParts = trimmed.Split('.');
        return JwsParseResult.Fail(
            $"Expected 3 parts, got {dotParts.Length}. Compact JWS must be header.payload.signature.");
    }

    /// <summary>
    /// True when <paramref name="value"/> is already a 3-part JWS (padding / alphabet may still need normalizing).
    /// </summary>
    public static bool LooksLikeCompactJws(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        return TryParseCompactJws(value.Trim(), requireJsonHeader: false, out _, out _);
    }

    private static bool TryParseCompactJws(
        string trimmed,
        bool requireJsonHeader,
        out string compactJws,
        out string? error)
    {
        compactJws = string.Empty;
        error = null;

        var parts = trimmed.Split('.');
        if (parts.Length != 3)
        {
            error = $"Expected 3 parts, got {parts.Length}";
            return false;
        }

        if (parts.Any(string.IsNullOrEmpty))
        {
            error = "Compact JWS contains an empty part.";
            return false;
        }

        try
        {
            string[] normalized =
            [
                TseCryptoHelper.NormalizeToBase64UrlNoPadding(parts[0]),
                TseCryptoHelper.NormalizeToBase64UrlNoPadding(parts[1]),
                TseCryptoHelper.NormalizeToBase64UrlNoPadding(parts[2])
            ];

            if (requireJsonHeader && !HeaderLooksLikeJson(normalized[0]))
            {
                error = "JWS header is not JSON.";
                return false;
            }

            compactJws = $"{normalized[0]}.{normalized[1]}.{normalized[2]}";
            return true;
        }
        catch (TsePipelineException ex)
        {
            if (requireJsonHeader)
            {
                error = ex.Message;
                return false;
            }

            // Three-part shell (e.g. tests using "a.b.c") — keep as compact JWS shape.
            compactJws = trimmed;
            return true;
        }
    }

    private static bool TryExtractTrailingCompactJws(string qrWire, out string compactJws)
    {
        compactJws = string.Empty;
        for (var i = qrWire.Length - 1; i >= 0; i--)
        {
            if (qrWire[i] != '_')
                continue;

            var candidate = qrWire[(i + 1)..];
            if (TryParseCompactJws(candidate, requireJsonHeader: true, out compactJws, out _))
                return true;
        }

        return false;
    }

    private static bool TryReconstructFromFiskalyMachineCode(
        string qr,
        out string compactJws,
        out string? error)
    {
        compactJws = string.Empty;
        error = null;

        var segments = qr.Split('_', StringSplitOptions.None)
            .Where(s => s.Length > 0)
            .ToArray();

        if (segments.Length < FiskalySigningSegmentCount + 1)
        {
            error =
                $"RKSV machine code has {segments.Length} segments; expected at least {FiskalySigningSegmentCount + 1} " +
                "(R1-ATx, 11 body fields, Sig-Wert). Compact JWS must have 3 parts (header.payload.signature).";
            return false;
        }

        var dataToBeSigned = "_" + string.Join("_", segments.Take(FiskalySigningSegmentCount));
        var sigWert = string.Join("_", segments.Skip(FiskalySigningSegmentCount));
        if (string.IsNullOrWhiteSpace(sigWert))
        {
            error = "Fiskaly QR signature segment is empty.";
            return false;
        }

        byte[] signatureBytes;
        try
        {
            signatureBytes = TseCryptoHelper.FromBase64UrlOrStd(sigWert);
        }
        catch (TsePipelineException ex)
        {
            error = $"Fiskaly Sig-Wert is not valid Base64/Base64URL: {ex.Message}";
            return false;
        }

        if (signatureBytes.Length == 0)
        {
            error = "Fiskaly Sig-Wert decoded to an empty signature.";
            return false;
        }

        var headerB64 = TseCryptoHelper.ToBase64UrlNoPadding(RksvJwsHeaderUtf8);
        var payloadB64 = TseCryptoHelper.ToBase64UrlNoPadding(Encoding.UTF8.GetBytes(dataToBeSigned));
        var signatureB64 = TseCryptoHelper.ToBase64UrlNoPadding(signatureBytes);
        compactJws = $"{headerB64}.{payloadB64}.{signatureB64}";
        return true;
    }

    private static bool HeaderLooksLikeJson(string headerB64Url)
    {
        try
        {
            var bytes = TseCryptoHelper.FromBase64UrlNoPadding(headerB64Url);
            var text = Encoding.UTF8.GetString(bytes);
            return text.TrimStart().StartsWith('{');
        }
        catch
        {
            return false;
        }
    }
}
