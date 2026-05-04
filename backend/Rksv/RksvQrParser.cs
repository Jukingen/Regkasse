using System.Text;
using System.Text.RegularExpressions;
using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Rksv;

/// <summary>
/// Pure RKSV QR string parser (no DB, no signature cryptography). Supports Regkasse compact layout and a 11+JWS standard-style layout.
/// </summary>
public static class RksvQrParser
{
    /// <summary>RKSV standard five gross bucket codes (positions 4–8 in <see cref="RksvQrPayloadLayout.StandardRksvV1"/>).</summary>
    public static readonly IReadOnlyList<string> StandardTaxBucketCodes = new[]
    {
        "standard", "reduced_1", "reduced_2", "zero", "special"
    };

    /// <summary>Synthetic bucket codes for <see cref="RksvQrPayloadLayout.InternalCompact"/>.</summary>
    public const string InternalCompactPrimaryCode = "internal_compact_primary";
    public const string InternalCompactSecondaryCode = "internal_compact_secondary";

    private static readonly Regex PrefixRegex = new(
        @"^_((?<algo>R1-AT\d+))_(?<remainder>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Parses an RKSV-style QR payload into <see cref="RksvQrPayload"/> or returns errors.</summary>
    public static RksvQrParseResult Parse(string? payload)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(payload))
        {
            errors.Add("Payload is null or whitespace.");
            return RksvQrParseResult.Fail(errors);
        }

        var trimmed = payload.Trim();
        var prefixMatch = PrefixRegex.Match(trimmed);
        if (!prefixMatch.Success)
        {
            errors.Add("Payload must start with '_R1-AT{version}_' (RKSV machine-readable prefix).");
            return RksvQrParseResult.Fail(errors);
        }

        var algorithmId = prefixMatch.Groups["algo"].Value;
        var remainder = prefixMatch.Groups["remainder"].Value;
        if (string.IsNullOrEmpty(remainder))
        {
            errors.Add("Payload is missing fields after algorithm id.");
            return RksvQrParseResult.Fail(errors);
        }

        if (!TrySplitBodyAndJws(remainder, out var body, out var signature, out var splitError))
        {
            errors.Add(splitError);
            return RksvQrParseResult.Fail(errors);
        }

        if (!IsJwsShell(signature, out var jwsErrors))
        {
            errors.AddRange(jwsErrors);
            return RksvQrParseResult.Fail(errors);
        }

        var bodyParts = body.Split('_');
        if (bodyParts.Length == 6)
            return BuildInternalCompact(algorithmId, bodyParts, signature, errors);

        if (bodyParts.Length == 11)
            return BuildStandardRksvV1(algorithmId, bodyParts, signature, errors);

        errors.Add(
            $"Unsupported field layout: expected 6 body segments (internal compact) or 11 (standard RKSV-style) before signature, found {bodyParts.Length}.");
        return RksvQrParseResult.Fail(errors);
    }

    /// <summary>Parses the payload or throws <see cref="RksvQrParseException"/>.</summary>
    public static RksvQrPayload ParseOrThrow(string? payload)
    {
        var r = Parse(payload);
        if (r.Success && r.Payload != null)
            return r.Payload;
        throw new RksvQrParseException(r.Errors);
    }

    private static RksvQrParseResult BuildInternalCompact(
        string algorithmId,
        string[] bodyParts,
        string signature,
        List<string> errors)
    {
        var kassen = bodyParts[0];
        var beleg = bodyParts[1];
        var ts = bodyParts[2];
        var a1 = bodyParts[3];
        var a2 = bodyParts[4];
        var cert = bodyParts[5];

        if (string.IsNullOrWhiteSpace(kassen)) errors.Add("Cash register id segment is empty.");
        if (string.IsNullOrWhiteSpace(beleg)) errors.Add("Receipt number segment is empty.");
        if (string.IsNullOrWhiteSpace(ts)) errors.Add("Timestamp segment is empty.");
        if (string.IsNullOrWhiteSpace(a1)) errors.Add("First amount segment is empty.");
        if (string.IsNullOrWhiteSpace(a2)) errors.Add("Second amount segment is empty.");
        if (string.IsNullOrWhiteSpace(cert)) errors.Add("Certificate serial segment is empty.");

        if (errors.Count > 0)
            return RksvQrParseResult.Fail(errors);

        var buckets = new List<RksvQrTaxBucket>
        {
            new(InternalCompactPrimaryCode, a1.Trim()),
            new(InternalCompactSecondaryCode, a2.Trim())
        };

        return RksvQrParseResult.Ok(new RksvQrPayload
        {
            AlgorithmId = algorithmId,
            Layout = RksvQrPayloadLayout.InternalCompact,
            CashRegisterId = kassen.Trim(),
            ReceiptNumber = beleg.Trim(),
            Timestamp = ts.Trim(),
            TaxBuckets = buckets,
            EncryptedTurnoverCounter = null,
            CertificateSerial = cert.Trim(),
            PreviousSignature = null,
            Signature = signature
        });
    }

    private static RksvQrParseResult BuildStandardRksvV1(
        string algorithmId,
        string[] bodyParts,
        string signature,
        List<string> errors)
    {
        var kassen = bodyParts[0];
        var beleg = bodyParts[1];
        var ts = bodyParts[2];
        var buckets = new List<RksvQrTaxBucket>(5);
        for (var i = 0; i < 5; i++)
        {
            var raw = bodyParts[3 + i];
            if (string.IsNullOrWhiteSpace(raw))
                errors.Add($"Tax bucket '{StandardTaxBucketCodes[i]}' is empty.");
            buckets.Add(new RksvQrTaxBucket(StandardTaxBucketCodes[i], raw.Trim()));
        }

        var enc = bodyParts[8];
        var cert = bodyParts[9];
        var prev = bodyParts[10];

        if (string.IsNullOrWhiteSpace(kassen)) errors.Add("Cash register id segment is empty.");
        if (string.IsNullOrWhiteSpace(beleg)) errors.Add("Receipt number segment is empty.");
        if (string.IsNullOrWhiteSpace(ts)) errors.Add("Timestamp segment is empty.");
        if (string.IsNullOrWhiteSpace(enc)) errors.Add("Encrypted turnover counter segment is empty.");
        if (string.IsNullOrWhiteSpace(cert)) errors.Add("Certificate serial segment is empty.");
        if (string.IsNullOrWhiteSpace(prev)) errors.Add("Previous signature segment is empty.");

        if (errors.Count > 0)
            return RksvQrParseResult.Fail(errors);

        return RksvQrParseResult.Ok(new RksvQrPayload
        {
            AlgorithmId = algorithmId,
            Layout = RksvQrPayloadLayout.StandardRksvV1,
            CashRegisterId = kassen.Trim(),
            ReceiptNumber = beleg.Trim(),
            Timestamp = ts.Trim(),
            TaxBuckets = buckets,
            EncryptedTurnoverCounter = enc.Trim(),
            CertificateSerial = cert.Trim(),
            PreviousSignature = prev.Trim(),
            Signature = signature
        });
    }

    private static bool IsJwsShell(string compactJws, out List<string> errors)
    {
        errors = new List<string>();
        var segments = compactJws.Split('.');
        if (segments.Length != 3)
        {
            errors.Add("Signature must be a compact JWS with exactly three dot-separated parts.");
            return false;
        }

        for (var i = 0; i < segments.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(segments[i]))
                errors.Add($"JWS part {i + 1} is empty.");
        }

        return errors.Count == 0;
    }

    /// <summary>Rejects amount-like strings that happen to contain two dots but are not JWTs.</summary>
    private static bool JwtHeaderSegmentLooksLikeJson(string compactJws)
    {
        var header = compactJws.Split('.')[0];
        try
        {
            var bytes = TseCryptoHelper.FromBase64UrlNoPadding(header);
            var text = Encoding.UTF8.GetString(bytes);
            return text.TrimStart().StartsWith("{", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Finds compact JWS by trying each underscore split: body must have 6 or 11 segments, JWS must have three parts,
    /// and the JWT header must Base64URL-decode to JSON. The rightmost qualifying underscore wins.
    /// </summary>
    private static bool TrySplitBodyAndJws(string remainder, out string body, out string jws, out string error)
    {
        body = string.Empty;
        jws = string.Empty;
        error = string.Empty;

        for (var i = remainder.Length - 1; i >= 0; i--)
        {
            if (remainder[i] != '_')
                continue;

            var candidateJws = remainder[(i + 1)..];
            if (string.IsNullOrWhiteSpace(candidateJws))
                continue;

            if (!IsJwsShell(candidateJws, out _))
                continue;

            if (!JwtHeaderSegmentLooksLikeJson(candidateJws))
                continue;

            var candidateBody = remainder[..i];
            if (string.IsNullOrEmpty(candidateBody))
                continue;

            var n = candidateBody.Split('_').Length;
            if (n is not (6 or 11))
                continue;

            body = candidateBody;
            jws = candidateJws;
            return true;
        }

        error = "Payload must contain a compact JWS (three dot-separated segments) after exactly 6 or 11 underscore-separated body fields.";
        return false;
    }
}
