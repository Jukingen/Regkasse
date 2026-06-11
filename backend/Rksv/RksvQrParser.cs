using System.Text;
using System.Text.RegularExpressions;
using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Rksv;

/// <summary>
/// Pure RKSV QR string parser (no DB, no signature cryptography).
/// Prefers BMF §9 <see cref="RksvQrPayloadLayout.StandardRksvV1"/> (11 body fields + JWS),
/// with legacy <see cref="RksvQrPayloadLayout.InternalCompact"/> fallback during transition.
/// </summary>
public static class RksvQrParser
{
    /// <summary>Body field count for BMF §9 machine code after the algorithm prefix.</summary>
    public const int StandardRksvV1BodySegmentCount = 11;

    /// <summary>Body field count for legacy internal compact QR.</summary>
    public const int InternalCompactBodySegmentCount = 6;

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

    /// <summary>
    /// Returns true when the payload can be split into 11 RKSV §9 body segments followed by a compact JWS.
    /// </summary>
    public static bool IsStandardRksvV1Format(string? qrPayload) =>
        TryResolvePrefix(qrPayload, out _, out var remainder)
        && TrySplitBodyAndJws(remainder, StandardRksvV1BodySegmentCount, out _, out _, out _);

    /// <summary>
    /// Returns true when the payload can be split into 6 legacy internal-compact body segments followed by a compact JWS.
    /// </summary>
    public static bool IsInternalCompactFormat(string? qrPayload) =>
        TryResolvePrefix(qrPayload, out _, out var remainder)
        && TrySplitBodyAndJws(remainder, InternalCompactBodySegmentCount, out _, out _, out _);

    /// <summary>Parses an RKSV-style QR payload into <see cref="RksvQrPayload"/> or returns errors.</summary>
    public static RksvQrParseResult Parse(string? payload)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(payload))
        {
            errors.Add("Payload is null or whitespace.");
            return RksvQrParseResult.Fail(errors);
        }

        if (!TryResolvePrefix(payload, out var algorithmId, out var remainder))
        {
            errors.Add("Payload must start with '_R1-AT{version}_' (RKSV machine-readable prefix).");
            return RksvQrParseResult.Fail(errors);
        }

        if (string.IsNullOrEmpty(remainder))
        {
            errors.Add("Payload is missing fields after algorithm id.");
            return RksvQrParseResult.Fail(errors);
        }

        // Prefer BMF §9 (11 body segments + compact JWS) during transition.
        if (TrySplitBodyAndJws(remainder, StandardRksvV1BodySegmentCount, out var standardBody, out var standardSignature, out _))
        {
            if (!IsJwsShell(standardSignature, out var jwsErrors))
                return RksvQrParseResult.Fail(jwsErrors);

            return ParseStandardRksvV1(algorithmId, standardBody, standardSignature);
        }

        // Legacy internal compact (6 body segments + compact JWS).
        if (TrySplitBodyAndJws(remainder, InternalCompactBodySegmentCount, out var compactBody, out var compactSignature, out var splitError))
        {
            if (!IsJwsShell(compactSignature, out var jwsErrors))
                return RksvQrParseResult.Fail(jwsErrors);

            return ParseInternalCompact(algorithmId, compactBody, compactSignature);
        }

        errors.Add(splitError);
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

    private static bool TryResolvePrefix(string? payload, out string algorithmId, out string remainder)
    {
        algorithmId = string.Empty;
        remainder = string.Empty;
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        var prefixMatch = PrefixRegex.Match(payload.Trim());
        if (!prefixMatch.Success)
            return false;

        algorithmId = prefixMatch.Groups["algo"].Value;
        remainder = prefixMatch.Groups["remainder"].Value;
        return true;
    }

    private static RksvQrParseResult ParseInternalCompact(
        string algorithmId,
        string body,
        string signature)
    {
        var errors = new List<string>();
        var bodyParts = body.Split('_');
        if (bodyParts.Length != InternalCompactBodySegmentCount)
        {
            errors.Add(
                $"Legacy internal compact layout expected {InternalCompactBodySegmentCount} body segments, found {bodyParts.Length}.");
            return RksvQrParseResult.Fail(errors);
        }

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

    private static RksvQrParseResult ParseStandardRksvV1(
        string algorithmId,
        string body,
        string signature)
    {
        var errors = new List<string>();
        var bodyParts = body.Split('_');
        if (bodyParts.Length != StandardRksvV1BodySegmentCount)
        {
            errors.Add(
                $"BMF §9 layout expected {StandardRksvV1BodySegmentCount} body segments, found {bodyParts.Length}.");
            return RksvQrParseResult.Fail(errors);
        }

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
    /// Finds compact JWS by scanning underscore splits right-to-left. Only accepts splits whose body has
    /// exactly <paramref name="requiredBodySegmentCount"/> underscore-separated fields.
    /// </summary>
    private static bool TrySplitBodyAndJws(
        string remainder,
        int requiredBodySegmentCount,
        out string body,
        out string jws,
        out string error)
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

            if (candidateBody.Split('_').Length != requiredBodySegmentCount)
                continue;

            body = candidateBody;
            jws = candidateJws;
            return true;
        }

        error =
            $"Payload must contain a compact JWS (three dot-separated segments) after exactly {requiredBodySegmentCount} underscore-separated body fields.";
        return false;
    }
}
