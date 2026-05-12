using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QRCoder;
using QRCoder.Exceptions;

namespace KasseAPI_Final.Services;

/// <summary>
/// QR payload'tan PNG/SVG görsel üretir. Memory cache ile optimize.
/// </summary>
public class QrImageService : IQrImageService
{
    private const int PngSizePx = 256;
    private const int PixelsPerModule = 8;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    private const int MaxCashRegisterChars = 12;
    private const int SignatureHeadChars = 8;
    private const int SignatureTailChars = 8;
    private const int MaxReceiptCharsLayer1 = 40;
    private const int MaxCertSerialCharsLayer1 = 24;
    private const int MinQrVersion = 10;
    private const int MaxQrVersion = 20;
    /// <summary>UTF-8 byte cap when QR still does not fit at max version (below typical ~2331 byte mode limit).</summary>
    private const int PngPayloadMaxUtf8Bytes = 2200;

    private static readonly Regex RksvPrefixRegex = new(
        @"^_((?<algo>R1-AT\d+))_(?<remainder>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IPaymentService _paymentService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<QrImageService> _logger;
    private readonly AppDbContext _db;

    public QrImageService(
        IPaymentService paymentService,
        IMemoryCache cache,
        ILogger<QrImageService> logger,
        AppDbContext db)
    {
        _paymentService = paymentService;
        _cache = cache;
        _logger = logger;
        _db = db;
    }

    public async Task<byte[]?> GetQrPngAsync(Guid paymentId, CancellationToken ct = default)
    {
        var payloadResult = await _paymentService.GetQrPayloadForPaymentAsync(paymentId);
        if (payloadResult is not { } result || string.IsNullOrEmpty(result.QrPayload))
        {
            _logger.LogDebug("No QR payload for payment {PaymentId}", paymentId);
            return null;
        }

        var (qrPayload, updatedAt) = result;
        var cacheKey = GetCacheKey(paymentId, "png", updatedAt);

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var voucherGross = await GetVoucherRedemptionGrossAsync(paymentId, ct).ConfigureAwait(false);
            return GeneratePng(qrPayload, voucherGross);
        })!;
    }

    public async Task<string?> GetQrSvgAsync(Guid paymentId, CancellationToken ct = default)
    {
        var payloadResult = await _paymentService.GetQrPayloadForPaymentAsync(paymentId);
        if (payloadResult is not { } result || string.IsNullOrEmpty(result.QrPayload))
        {
            _logger.LogDebug("No QR payload for payment {PaymentId}", paymentId);
            return null;
        }

        var (qrPayload, updatedAt) = result;
        var cacheKey = GetCacheKey(paymentId, "svg", updatedAt);

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var voucherGross = await GetVoucherRedemptionGrossAsync(paymentId, ct).ConfigureAwait(false);
            return GenerateSvg(qrPayload, voucherGross);
        })!;
    }

    private async Task<decimal> GetVoucherRedemptionGrossAsync(Guid paymentId, CancellationToken ct)
    {
        var sum = await _db.VoucherLedgerEntries.AsNoTracking()
            .Where(l => l.PaymentId == paymentId && l.Type == VoucherTransactionType.Redeem)
            .SumAsync(l => (decimal?)l.Amount, ct)
            .ConfigureAwait(false);
        return sum.HasValue && sum.Value < 0 ? decimal.Round(-sum.Value, 2, MidpointRounding.AwayFromZero) : 0m;
    }

    private static string GetCacheKey(Guid paymentId, string format, DateTime? updatedAt)
        => $"qr:{paymentId}:{format}:{updatedAt?.Ticks ?? 0}";

    /// <summary>
    /// Generates PNG bytes: layered RKSV candidates, then QR versions 10–20 (then 1–9), ECC M/L; last resort UTF-8 truncate to 2200 bytes and retry same version sweep.
    /// </summary>
    /// <inheritdoc />
    public byte[]? GetQrPngFromExactPayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        var text = payload.Trim();
        var png = TryEncodePayloadToPng(text);
        if (png != null)
            return png;

        var truncated = TruncateStringToUtf8ByteLength(text, PngPayloadMaxUtf8Bytes);
        if (truncated.Length > 0 && !string.Equals(truncated, text, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "QR reprint PNG: UTF-8 truncation to {MaxBytes} bytes after version sweep; original length {OriginalLen} chars",
                PngPayloadMaxUtf8Bytes,
                text.Length);
            var pngTrunc = TryEncodePayloadToPng(truncated);
            if (pngTrunc != null)
                return pngTrunc;
        }

        _logger.LogError("QR reprint PNG encoding failed for payload length {Len}", text.Length);
        return null;
    }

    /// <summary>Single-string QR PNG (version / ECC sweep). Used for fiscal reprint from stored payload only.</summary>
    private static byte[]? TryEncodePayloadToPng(string text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        foreach (var ecc in new[] { QRCodeGenerator.ECCLevel.M, QRCodeGenerator.ECCLevel.L })
        {
            for (var version = MinQrVersion; version <= MaxQrVersion; version++)
            {
                try
                {
                    using var gen = new QRCodeGenerator();
                    using var qrData = gen.CreateQrCode(text, ecc, false, false, QRCodeGenerator.EciMode.Default, version);
                    using var pngQr = new PngByteQRCode(qrData);
                    return pngQr.GetGraphic(PixelsPerModule, drawQuietZones: true);
                }
                catch (DataTooLongException)
                {
                    // next version
                }
            }

            for (var version = 1; version < MinQrVersion; version++)
            {
                try
                {
                    using var gen = new QRCodeGenerator();
                    using var qrData = gen.CreateQrCode(text, ecc, false, false, QRCodeGenerator.EciMode.Default, version);
                    using var pngQr = new PngByteQRCode(qrData);
                    return pngQr.GetGraphic(PixelsPerModule, drawQuietZones: true);
                }
                catch (DataTooLongException)
                {
                    // next version
                }
            }
        }

        return null;
    }

    private byte[] GeneratePng(string payload, decimal voucherRedemptionGross)
    {
        foreach (var candidate in BuildEncodingCandidates(payload, voucherRedemptionGross))
        {
            var png = TryEncodePayloadToPng(candidate);
            if (png != null)
                return png;
        }

        var truncated = TruncateStringToUtf8ByteLength(payload, PngPayloadMaxUtf8Bytes);
        if (truncated.Length > 0 && !string.Equals(truncated, payload, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "QR PNG: UTF-8 truncation to {MaxBytes} bytes after version 1–20 sweep; original length {OriginalLen} chars",
                PngPayloadMaxUtf8Bytes,
                payload.Length);
            var pngTrunc = TryEncodePayloadToPng(truncated);
            if (pngTrunc != null)
                return pngTrunc;
        }

        _logger.LogError("QR PNG encoding failed after compaction, version sweep (1–20), and UTF-8 truncation for payload length {Len}", payload.Length);
        throw new InvalidOperationException("QR code could not be generated: payload remains too large after compaction.");
    }

    /// <summary>
    /// Truncates so that the UTF-8 encoding length does not exceed <paramref name="maxUtf8Bytes"/> (valid UTF-8 boundary).
    /// </summary>
    private static string TruncateStringToUtf8ByteLength(string s, int maxUtf8Bytes)
    {
        if (string.IsNullOrEmpty(s) || maxUtf8Bytes <= 0)
            return string.Empty;

        var bytes = Encoding.UTF8.GetBytes(s);
        if (bytes.Length <= maxUtf8Bytes)
            return s;

        var len = maxUtf8Bytes;
        while (len > 0 && (bytes[len - 1] & 0xC0) == 0x80)
            len--;

        return len == 0 ? string.Empty : Encoding.UTF8.GetString(bytes, 0, len);
    }

    private string GenerateSvg(string payload, decimal voucherRedemptionGross)
    {
        foreach (var candidate in BuildEncodingCandidates(payload, voucherRedemptionGross))
        {
            if (TryCreateQrCodeData(candidate, out var qrData))
            {
                using (qrData)
                using (var svgQr = new SvgQRCode(qrData))
                    return svgQr.GetGraphic(new System.Drawing.Size(PngSizePx, PngSizePx), drawQuietZones: true);
            }
        }

        _logger.LogError("QR SVG encoding failed after all compaction layers for payload length {Len}", payload.Length);
        throw new InvalidOperationException("QR code could not be generated: payload remains too large after compaction.");
    }

    /// <summary>
    /// Ordered candidates: raw, layer-1 compressed wire format, layer-3 mandatory-only mini payload.
    /// </summary>
    private static IEnumerable<string> BuildEncodingCandidates(string payload, decimal voucherGross)
    {
        yield return payload;

        var layer1 = CompressLongFields(payload);
        if (!string.Equals(layer1, payload, StringComparison.Ordinal))
            yield return layer1;

        var layer3 = BuildMinimalMandatoryPayload(payload, voucherGross);
        if (!string.IsNullOrEmpty(layer3)
            && !string.Equals(layer3, payload, StringComparison.Ordinal)
            && !string.Equals(layer3, layer1, StringComparison.Ordinal))
            yield return layer3;
    }

    /// <summary>
    /// Layer 1: shorten cash register id, receipt/cert where needed, abbreviate compact JWS (first/last 8 chars).
    /// </summary>
    private static string CompressLongFields(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return payload;

        var trimmed = payload.Trim();
        var prefixMatch = RksvPrefixRegex.Match(trimmed);
        if (!prefixMatch.Success)
            return trimmed;

        var remainder = prefixMatch.Groups["remainder"].Value;
        if (!TrySplitBodyAndJws(remainder, out var body, out var signature))
            return trimmed;

        var abbreviatedSig = AbbreviateCompactJws(signature);
        var parts = body.Split('_');
        if (parts.Length == 6)
        {
            parts[0] = TruncateRegisterId(parts[0]);
            parts[1] = TruncateReceipt(parts[1]);
            parts[5] = TruncateCertOrOpaque(parts[5], MaxCertSerialCharsLayer1);
        }
        else if (parts.Length == 11)
        {
            parts[0] = TruncateRegisterId(parts[0]);
            parts[1] = TruncateReceipt(parts[1]);
            parts[8] = TruncateOpaqueMiddle(parts[8], 48);
            parts[9] = TruncateCertOrOpaque(parts[9], MaxCertSerialCharsLayer1);
            parts[10] = AbbreviateOpaqueSignatureLike(parts[10]);
        }

        var algo = prefixMatch.Groups["algo"].Value;
        var newBody = string.Join("_", parts);
        return $"_{algo}_{newBody}_{abbreviatedSig}";
    }

    /// <summary>
    /// Layer 3: RKSV mandatory-style compact row K,R,D,A,S plus optional G (voucher gross).
    /// Drops detailed tax buckets, certificate chain fields, and non-fiscal extras.
    /// </summary>
    private static string BuildMinimalMandatoryPayload(string payload, decimal voucherGross)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return string.Empty;

        var trimmed = payload.Trim();
        if (trimmed.StartsWith("NON_FISCAL", StringComparison.OrdinalIgnoreCase))
            return BuildMinimalNonFiscal(trimmed, voucherGross);

        var parsed = RksvQrParser.Parse(trimmed);
        if (!parsed.Success || parsed.Payload == null)
            return string.Empty;

        var p = parsed.Payload;
        var k = TruncateRegisterId(p.CashRegisterId);
        var r = TruncateReceipt(p.ReceiptNumber);
        var d = p.Timestamp;
        var a = ComputeTotalAmountString(p);
        var s = AbbreviateCompactJws(p.Signature);

        var sb = new StringBuilder(256);
        sb.Append("_R1-MINI|K:").Append(k)
            .Append("|R:").Append(r)
            .Append("|D:").Append(d)
            .Append("|A:").Append(a)
            .Append("|S:").Append(s);
        if (voucherGross > 0.009m)
            sb.Append("|G:").Append(voucherGross.ToString("F2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    private static string BuildMinimalNonFiscal(string trimmed, decimal voucherGross)
    {
        var parts = trimmed.Split('_', StringSplitOptions.None);
        if (parts.Length < 4)
            return trimmed;

        var receipt = parts.Length > 1 ? parts[1] : "";
        var dt = parts.Length > 2 ? parts[2] : "";
        var amt = parts.Length > 3 ? parts[3] : "";
        var sb = new StringBuilder(128);
        sb.Append("_NF-MINI|R:").Append(TruncateReceipt(receipt))
            .Append("|D:").Append(dt)
            .Append("|A:").Append(amt);
        if (voucherGross > 0.009m)
            sb.Append("|G:").Append(voucherGross.ToString("F2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    private static string ComputeTotalAmountString(RksvQrPayload p)
    {
        if (p.TaxBuckets.Count == 0)
            return "0.00";

        decimal sum = 0;
        foreach (var b in p.TaxBuckets)
        {
            if (decimal.TryParse(b.Amount.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var g))
                sum += g;
        }

        return sum.ToString("F2", CultureInfo.InvariantCulture);
    }

    private static string TruncateRegisterId(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        var v = value.Trim();
        return v.Length <= MaxCashRegisterChars ? v : v[..MaxCashRegisterChars];
    }

    private static string TruncateReceipt(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        var v = value.Trim();
        if (v.Length <= MaxReceiptCharsLayer1)
            return v;
        var head = (MaxReceiptCharsLayer1 / 2) - 1;
        var tail = MaxReceiptCharsLayer1 - head - 2;
        return string.Concat(v.AsSpan(0, head), "..", v.AsSpan(v.Length - tail));
    }

    private static string TruncateCertOrOpaque(string value, int maxLen)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        var v = value.Trim();
        return v.Length <= maxLen ? v : string.Concat(v.AsSpan(0, maxLen - 3), "...");
    }

    private static string TruncateOpaqueMiddle(string value, int maxLen)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        var v = value.Trim();
        if (v.Length <= maxLen)
            return v;
        var keep = (maxLen / 2) - 1;
        return string.Concat(v.AsSpan(0, keep), "..", v.AsSpan(v.Length - keep));
    }

    private static string AbbreviateOpaqueSignatureLike(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        var v = value.Trim();
        if (v.Length <= SignatureHeadChars + SignatureTailChars + 2)
            return v;
        return string.Concat(v.AsSpan(0, SignatureHeadChars), "..", v.AsSpan(v.Length - SignatureTailChars));
    }

    private static string AbbreviateCompactJws(string compactJws)
    {
        if (string.IsNullOrEmpty(compactJws))
            return compactJws;
        var v = compactJws.Trim();
        var segments = v.Split('.');
        if (segments.Length != 3)
            return AbbreviateOpaqueSignatureLike(v);

        if (v.Length <= SignatureHeadChars + SignatureTailChars + 2)
            return v;

        var head = v[..Math.Min(SignatureHeadChars, v.Length)];
        var tailStart = Math.Max(0, v.Length - SignatureTailChars);
        var tail = v[tailStart..];
        return string.Concat(head, "..", tail);
    }

    /// <summary>
    /// Layer 2: try fixed QR versions 10–20 (ECC M, then ECC L as fallback).
    /// </summary>
    private static bool TryCreateQrCodeData(string text, out QRCodeData qrData)
    {
        qrData = null!;
        if (string.IsNullOrEmpty(text))
            return false;

        foreach (var ecc in new[] { QRCodeGenerator.ECCLevel.M, QRCodeGenerator.ECCLevel.L })
        {
            for (var version = MinQrVersion; version <= MaxQrVersion; version++)
            {
                try
                {
                    using var gen = new QRCodeGenerator();
                    qrData = gen.CreateQrCode(text, ecc, false, false, QRCodeGenerator.EciMode.Default, version);
                    return true;
                }
                catch (DataTooLongException)
                {
                    // try next version / ECC
                }
            }
        }

        return false;
    }

    private static bool TrySplitBodyAndJws(string remainder, out string body, out string jws)
    {
        body = string.Empty;
        jws = string.Empty;

        for (var i = remainder.Length - 1; i >= 0; i--)
        {
            if (remainder[i] != '_')
                continue;

            var candidateJws = remainder[(i + 1)..];
            if (string.IsNullOrWhiteSpace(candidateJws))
                continue;

            if (!IsJwsShell(candidateJws))
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

        return false;
    }

    private static bool IsJwsShell(string compactJws)
    {
        var segments = compactJws.Split('.');
        return segments.Length == 3
               && segments.All(s => !string.IsNullOrWhiteSpace(s));
    }

    private static bool JwtHeaderSegmentLooksLikeJson(string compactJws)
    {
        var header = compactJws.Split('.')[0];
        try
        {
            var bytes = TseCryptoHelper.FromBase64UrlNoPadding(header);
            var t = Encoding.UTF8.GetString(bytes);
            return t.TrimStart().StartsWith("{", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}
