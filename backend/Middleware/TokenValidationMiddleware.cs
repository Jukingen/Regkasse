using System.Text.Json;
using KasseAPI_Final.Services.Token;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Rejects access tokens that were blacklisted on logout (immediate revoke).
/// Runs before JWT authentication so revoked bearers never establish a principal.
/// Complements DB session invalidation via <see cref="IRefreshTokenService"/>.
/// </summary>
public sealed class TokenValidationMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly RequestDelegate _next;

    public TokenValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITokenBlacklistService blacklistService)
    {
        var token = ExtractBearerToken(context.Request.Headers.Authorization.ToString());
        if (!string.IsNullOrEmpty(token) && blacklistService.IsTokenBlacklisted(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(
                    new
                    {
                        success = false,
                        message = "Token has been revoked. Please login again.",
                        code = "TOKEN_REVOKED",
                    },
                    JsonOptions),
                context.RequestAborted).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    /// <summary>Exported for unit tests.</summary>
    internal static string? ExtractBearerToken(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return null;

        var header = authorizationHeader.Trim();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return header[7..].Trim();

        return header;
    }
}
