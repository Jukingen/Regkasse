namespace KasseAPI_Final.Services.AdminProducts;

/// <summary>
/// Detects JPG/PNG/WebP from magic bytes only (no trust in client-provided file name).
/// </summary>
public static class ProductImageFormatDetector
{
    public static bool TryDetect(ReadOnlySpan<byte> header, out string extension, out string contentType)
    {
        extension = "";
        contentType = "";

        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            extension = ".jpg";
            contentType = "image/jpeg";
            return true;
        }

        if (header.Length >= 8
            && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
            && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            extension = ".png";
            contentType = "image/png";
            return true;
        }

        // RIFF .... WEBP
        if (header.Length >= 12
            && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
            && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        {
            extension = ".webp";
            contentType = "image/webp";
            return true;
        }

        return false;
    }
}
