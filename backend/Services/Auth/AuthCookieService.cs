using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Auth;

public sealed class AuthCookieService : IAuthCookieService
{
    private readonly IOptionsMonitor<AuthCookieOptions> _options;
    private readonly IHostEnvironment _environment;

    public AuthCookieService(
        IOptionsMonitor<AuthCookieOptions> options,
        IHostEnvironment environment)
    {
        _options = options;
        _environment = environment;
    }

    public void AppendAuthCookies(
        HttpResponse response,
        string accessToken,
        string? refreshToken,
        DateTime accessExpiresUtc,
        DateTime? refreshExpiresUtc,
        string? clientApp = null)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
            return;

        var now = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(accessToken)
            && JwtCookieBudget.Utf8ByteCount(accessToken) <= JwtCookieBudget.ReadLimitBytes(null))
        {
            var accessMaxAge = MaxAgeFromExpiry(accessExpiresUtc, now, TimeSpan.FromHours(24));
            response.Cookies.Append(
                GetAccessCookieName(clientApp),
                accessToken,
                BuildCookieOptions(options, accessMaxAge, httpOnly: true));
        }

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var refreshMaxAge = MaxAgeFromExpiry(
                refreshExpiresUtc ?? now.AddDays(14),
                now,
                TimeSpan.FromDays(14));
            response.Cookies.Append(
                GetRefreshCookieName(clientApp),
                refreshToken,
                BuildCookieOptions(options, refreshMaxAge, httpOnly: true));
        }
    }

    public void ClearAuthCookies(HttpResponse response, string? clientApp = null)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
            return;

        var expired = BuildCookieOptions(options, TimeSpan.Zero, httpOnly: true);
        foreach (var name in EnumerateClearNames(options, clientApp))
            response.Cookies.Append(name, string.Empty, expired);
    }

    public string? ReadAccessToken(HttpRequest request, string? clientApp = null)
    {
        var resolved = ResolveClientApp(request, clientApp);
        var options = _options.CurrentValue;
        var names = new List<string>();
        if (IsAdmin(resolved) || resolved == null)
            names.Add(GetAccessCookieName(ClientAppPolicy.Admin));
        if (IsPos(resolved) || resolved == null)
            names.Add(GetAccessCookieName(ClientAppPolicy.Pos));
        names.AddRange(LegacyAccessNames(options));
        return ReadFirst(request, names);
    }

    public string? ReadRefreshToken(HttpRequest request, string? clientApp = null)
    {
        var resolved = ResolveClientApp(request, clientApp);
        var options = _options.CurrentValue;
        var names = new List<string>();
        if (IsAdmin(resolved) || resolved == null)
            names.Add(GetRefreshCookieName(ClientAppPolicy.Admin));
        if (IsPos(resolved) || resolved == null)
            names.Add(GetRefreshCookieName(ClientAppPolicy.Pos));
        names.AddRange(LegacyRefreshNames(options));
        return ReadFirst(request, names);
    }

    /// <summary>Admin → <c>rk_admin_access_token</c>; otherwise POS (<c>rk_pos_access_token</c>).</summary>
    public static string GetAccessCookieName(string? clientApp)
    {
        return IsAdmin(clientApp)
            ? AuthCookieOptions.AdminAccessCookieName
            : AuthCookieOptions.PosAccessCookieName;
    }

    /// <summary>Admin → <c>rk_admin_refresh_token</c>; otherwise POS (<c>rk_pos_refresh_token</c>).</summary>
    public static string GetRefreshCookieName(string? clientApp)
    {
        return IsAdmin(clientApp)
            ? AuthCookieOptions.AdminRefreshCookieName
            : AuthCookieOptions.PosRefreshCookieName;
    }

    internal CookieOptions BuildCookieOptions(AuthCookieOptions options, TimeSpan maxAge, bool httpOnly)
    {
        var sameSite = ParseSameSite(options.SameSite, _environment.IsDevelopment());
        var secure = options.Secure ?? !_environment.IsDevelopment();
        if (sameSite == SameSiteMode.None)
            secure = true;

        var cookie = new CookieOptions
        {
            HttpOnly = httpOnly,
            Secure = secure,
            SameSite = sameSite,
            IsEssential = true,
            Path = "/",
            MaxAge = maxAge,
        };

        var domain = options.Domain?.Trim();
        if (!string.IsNullOrEmpty(domain))
            cookie.Domain = domain;

        return cookie;
    }

    internal static string? ResolveClientApp(HttpRequest request, string? explicitClientApp)
    {
        if (IsKnownApp(explicitClientApp))
            return NormalizeApp(explicitClientApp);

        if (request.Headers.TryGetValue(ClientAppPolicy.AppContextHttpHeader, out var header)
            && IsKnownApp(header.ToString()))
        {
            return NormalizeApp(header.ToString());
        }

        var fromOrigin = InferAppFromHost(request.Headers.Origin.ToString());
        if (fromOrigin != null)
            return fromOrigin;

        return InferAppFromHost(request.Headers.Referer.ToString());
    }

    private static SameSiteMode ParseSameSite(string? value, bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(value))
            return isDevelopment ? SameSiteMode.None : SameSiteMode.Lax;
        if (string.Equals(value, "None", StringComparison.OrdinalIgnoreCase))
            return SameSiteMode.None;
        if (string.Equals(value, "Strict", StringComparison.OrdinalIgnoreCase))
            return SameSiteMode.Strict;
        return SameSiteMode.Lax;
    }

    private static TimeSpan MaxAgeFromExpiry(DateTime expiresUtc, DateTime nowUtc, TimeSpan fallback)
    {
        var remaining = expiresUtc.ToUniversalTime() - nowUtc;
        if (remaining <= TimeSpan.Zero)
            return fallback;
        return remaining;
    }

    private static IEnumerable<string> EnumerateClearNames(AuthCookieOptions options, string? clientApp)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (IsAdmin(clientApp))
        {
            names.Add(GetAccessCookieName(ClientAppPolicy.Admin));
            names.Add(GetRefreshCookieName(ClientAppPolicy.Admin));
        }
        else if (IsPos(clientApp))
        {
            names.Add(GetAccessCookieName(ClientAppPolicy.Pos));
            names.Add(GetRefreshCookieName(ClientAppPolicy.Pos));
        }
        else
        {
            names.Add(GetAccessCookieName(ClientAppPolicy.Admin));
            names.Add(GetRefreshCookieName(ClientAppPolicy.Admin));
            names.Add(GetAccessCookieName(ClientAppPolicy.Pos));
            names.Add(GetRefreshCookieName(ClientAppPolicy.Pos));
        }

        foreach (var legacy in LegacyAccessNames(options))
            names.Add(legacy);
        foreach (var legacy in LegacyRefreshNames(options))
            names.Add(legacy);

        return names;
    }

    private static string[] LegacyAccessNames(AuthCookieOptions options)
    {
        var configured = options.AccessCookieName?.Trim();
        if (string.IsNullOrEmpty(configured)
            || string.Equals(configured, AuthCookieOptions.AdminAccessCookieName, StringComparison.Ordinal)
            || string.Equals(configured, AuthCookieOptions.PosAccessCookieName, StringComparison.Ordinal))
        {
            return new[] { AuthCookieOptions.LegacyAccessCookieName };
        }

        if (string.Equals(configured, AuthCookieOptions.LegacyAccessCookieName, StringComparison.Ordinal))
            return new[] { AuthCookieOptions.LegacyAccessCookieName };

        return new[] { AuthCookieOptions.LegacyAccessCookieName, configured };
    }

    private static string[] LegacyRefreshNames(AuthCookieOptions options)
    {
        var configured = options.RefreshCookieName?.Trim();
        if (string.IsNullOrEmpty(configured)
            || string.Equals(configured, AuthCookieOptions.AdminRefreshCookieName, StringComparison.Ordinal)
            || string.Equals(configured, AuthCookieOptions.PosRefreshCookieName, StringComparison.Ordinal))
        {
            return new[] { AuthCookieOptions.LegacyRefreshCookieName };
        }

        if (string.Equals(configured, AuthCookieOptions.LegacyRefreshCookieName, StringComparison.Ordinal))
            return new[] { AuthCookieOptions.LegacyRefreshCookieName };

        return new[] { AuthCookieOptions.LegacyRefreshCookieName, configured };
    }

    private static string? ReadFirst(HttpRequest request, IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            var value = ReadCookie(request, name);
            if (value != null)
                return value;
        }

        return null;
    }

    private static string? ReadCookie(HttpRequest request, string name)
    {
        if (request.Cookies.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
            return value;
        return null;
    }

    private static bool IsAdmin(string? clientApp)
        => string.Equals(NormalizeApp(clientApp), ClientAppPolicy.Admin, StringComparison.Ordinal);

    private static bool IsPos(string? clientApp)
        => string.Equals(NormalizeApp(clientApp), ClientAppPolicy.Pos, StringComparison.Ordinal);

    private static bool IsKnownApp(string? clientApp)
        => IsAdmin(clientApp) || IsPos(clientApp);

    private static string? NormalizeApp(string? clientApp)
    {
        var trimmed = clientApp?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.ToLowerInvariant();
    }

    private static string? InferAppFromHost(string? originOrReferer)
    {
        if (string.IsNullOrWhiteSpace(originOrReferer))
            return null;

        if (!Uri.TryCreate(originOrReferer, UriKind.Absolute, out var uri))
            return null;

        var host = uri.Host;
        if (host.StartsWith("admin.", StringComparison.OrdinalIgnoreCase)
            || host.Contains("admin.regkasse", StringComparison.OrdinalIgnoreCase))
        {
            return ClientAppPolicy.Admin;
        }

        if (host.StartsWith("pos.", StringComparison.OrdinalIgnoreCase)
            || host.Contains("pos.regkasse", StringComparison.OrdinalIgnoreCase))
        {
            return ClientAppPolicy.Pos;
        }

        return null;
    }
}
