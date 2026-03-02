using System.Drawing;
using Microsoft.Extensions.Caching.Memory;
using QRCoder;

namespace KasseAPI_Final.Services;

/// <summary>
/// QR payload'tan PNG/SVG görsel üretir. Memory cache ile optimize.
/// </summary>
public class QrImageService : IQrImageService
{
    private const int PngSizePx = 256;
    private const int PixelsPerModule = 8;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    private readonly IPaymentService _paymentService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<QrImageService> _logger;

    public QrImageService(
        IPaymentService paymentService,
        IMemoryCache cache,
        ILogger<QrImageService> logger)
    {
        _paymentService = paymentService;
        _cache = cache;
        _logger = logger;
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

        return await _cache.GetOrCreateAsync(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return Task.FromResult(GeneratePng(qrPayload))!;
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

        return await _cache.GetOrCreateAsync(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return Task.FromResult(GenerateSvg(qrPayload))!;
        })!;
    }

    private static string GetCacheKey(Guid paymentId, string format, DateTime? updatedAt)
        => $"qr:{paymentId}:{format}:{updatedAt?.Ticks ?? 0}";

    private static byte[] GeneratePng(string payload)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        using var pngQr = new PngByteQRCode(qrData);
        return pngQr.GetGraphic(PixelsPerModule, drawQuietZones: true);
    }

    private static string GenerateSvg(string payload)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        using var svgQr = new SvgQRCode(qrData);
        return svgQr.GetGraphic(new Size(PngSizePx, PngSizePx), drawQuietZones: true);
    }
}
