using System.Security.Cryptography;
using System.Text;
using SkiaSharp;

namespace KasseAPI_Final.Services;

/// <summary>Builds simple square PNG bytes for demo import placeholders (no external APIs).</summary>
internal static class DemoProductPlaceholderImageGenerator
{
    private const int Edge = 240;

    internal static byte[] CreateCategoryPlaceholderPng(string categoryName, string productName)
    {
        var (bg, accent) = ColorsForKey(categoryName);
        using var image = new SKBitmap(new SKImageInfo(Edge, Edge, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        image.Erase(bg);

        var plateSize = (int)(Edge * 0.55);
        var plateX = (Edge - plateSize) / 2;
        var plateY = (int)(Edge * 0.22);
        FillRect(image, plateX, plateY, plateSize, plateSize, accent);
        FillRect(image, plateX + 8, plateY + 8, plateSize - 16, plateSize - 16, Blend(bg, accent, 0.35f));

        DrawCategoryStrip(image, categoryName, accent);

        return EncodePng(image);
    }

    internal static byte[] CreateDefaultFoodPng()
    {
        var bg = new SKColor(255, 167, 38);
        var cream = new SKColor(255, 243, 224);
        using var image = new SKBitmap(new SKImageInfo(Edge, Edge, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        image.Erase(bg);

        var plateSize = (int)(Edge * 0.62);
        var plateX = (Edge - plateSize) / 2;
        var plateY = (Edge - plateSize) / 2;
        FillRect(image, plateX, plateY, plateSize, plateSize, cream);
        FillRect(image, plateX + 12, plateY + 12, plateSize - 24, plateSize - 24, Blend(bg, cream, 0.25f));

        return EncodePng(image);
    }

    private static void DrawCategoryStrip(SKBitmap image, string categoryName, SKColor accent)
    {
        var label = TruncateCategoryLabel(categoryName);
        var barHeight = Math.Max(28, Edge / 6);
        var y = Edge - barHeight;
        var barColor = new SKColor(accent.Red, accent.Green, accent.Blue, 235);
        FillRect(image, 0, y, Edge, barHeight, barColor);

        var x = 10;
        foreach (var ch in label.Take(12))
        {
            if (ch == ' ')
            {
                x += 8;
                continue;
            }

            FillRect(image, x, y + 8, 6, barHeight - 16, new SKColor(255, 255, 255));
            x += 9;
        }
    }

    private static void FillRect(SKBitmap image, int x, int y, int width, int height, SKColor color)
    {
        var maxX = Math.Min(Edge, x + width);
        var maxY = Math.Min(Edge, y + height);
        for (var py = Math.Max(0, y); py < maxY; py++)
        {
            for (var px = Math.Max(0, x); px < maxX; px++)
                image.SetPixel(px, py, color);
        }
    }

    private static SKColor Blend(SKColor a, SKColor b, float amount)
    {
        var t = Math.Clamp(amount, 0f, 1f);
        return new SKColor(
            (byte)Math.Round(a.Red * (1 - t) + b.Red * t),
            (byte)Math.Round(a.Green * (1 - t) + b.Green * t),
            (byte)Math.Round(a.Blue * (1 - t) + b.Blue * t));
    }

    private static string TruncateCategoryLabel(string categoryName)
    {
        var trimmed = categoryName.Trim();
        if (trimmed.Length <= 14)
            return trimmed.ToUpperInvariant();

        return trimmed[..14].ToUpperInvariant();
    }

    private static (SKColor Bg, SKColor Accent) ColorsForKey(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim().ToLowerInvariant()));
        var hue = (hash[0] + hash[1] * 256) % 360;
        var bg = HslToRgb(hue, 0.42, 0.72);
        var accent = HslToRgb(hue, 0.55, 0.48);
        return (bg, accent);
    }

    private static SKColor HslToRgb(double h, double s, double l)
    {
        h /= 360;
        double r, g, b;
        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            static double Hue2Rgb(double p, double q, double t)
            {
                if (t < 0)
                    t += 1;
                if (t > 1)
                    t -= 1;
                if (t < 1 / 6.0)
                    return p + (q - p) * 6 * t;
                if (t < 1 / 2.0)
                    return q;
                if (t < 2 / 3.0)
                    return p + (q - p) * (2 / 3.0 - t) * 6;
                return p;
            }

            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = Hue2Rgb(p, q, h + 1 / 3.0);
            g = Hue2Rgb(p, q, h);
            b = Hue2Rgb(p, q, h - 1 / 3.0);
        }

        return new SKColor(
            (byte)Math.Clamp((int)Math.Round(r * 255), 0, 255),
            (byte)Math.Clamp((int)Math.Round(g * 255), 0, 255),
            (byte)Math.Clamp((int)Math.Round(b * 255), 0, 255));
    }

    private static byte[] EncodePng(SKBitmap image)
    {
        using var snapshot = SKImage.FromBitmap(image);
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("PNG encode failed.");
        return encoded.ToArray();
    }
}
