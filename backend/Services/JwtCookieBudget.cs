using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services;

/// <summary>
/// FA <c>proxy.ts</c> mirrors the access JWT in a non-HttpOnly cookie. Browsers drop cookies above ~4KB.
/// Keep issued tokens under this budget (aligned with FA <c>MAX_ACCESS_TOKEN_COOKIE_CHARS</c> = 3500).
/// </summary>
public static class JwtCookieBudget
{
    public const int DefaultMaxUtf8Bytes = 3500;

    public static int ReadLimitBytes(IConfiguration? configuration)
    {
        var configured = configuration?.GetValue("JwtSettings:MaxTokenSizeBytes", DefaultMaxUtf8Bytes) ?? DefaultMaxUtf8Bytes;
        return configured > 0 ? configured : DefaultMaxUtf8Bytes;
    }

    public static int Utf8ByteCount(string token) => Encoding.UTF8.GetByteCount(token);

    public static void LogIfExceeded(ILogger logger, string token, IConfiguration? configuration = null)
    {
        var size = Utf8ByteCount(token);
        var limit = ReadLimitBytes(configuration);
        if (size > limit)
        {
            logger.LogWarning(
                "JWT token size {Size} bytes exceeds cookie budget {Budget} bytes (FA proxy cookie may be dropped)",
                size,
                limit);
        }
    }
}
