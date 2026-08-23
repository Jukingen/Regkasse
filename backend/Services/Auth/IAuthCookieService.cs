using Microsoft.AspNetCore.Http;

namespace KasseAPI_Final.Services.Auth;

/// <summary>
/// Issues and clears HttpOnly access/refresh cookies for browser sessions.
/// Cookie names are split by <c>clientApp</c> so FA and POS sessions in the same browser
/// do not overwrite each other. JSON token fields remain on login/refresh for POS/native.
/// </summary>
public interface IAuthCookieService
{
    void AppendAuthCookies(
        HttpResponse response,
        string accessToken,
        string? refreshToken,
        DateTime accessExpiresUtc,
        DateTime? refreshExpiresUtc,
        string? clientApp = null);

    /// <param name="clientApp">
    /// When <c>admin</c> or <c>pos</c>, expires that app's cookies plus legacy shared names.
    /// When null/unknown, expires both apps plus legacy (logout-all / unscoped).
    /// </param>
    void ClearAuthCookies(HttpResponse response, string? clientApp = null);

    string? ReadAccessToken(HttpRequest request, string? clientApp = null);

    string? ReadRefreshToken(HttpRequest request, string? clientApp = null);
}
