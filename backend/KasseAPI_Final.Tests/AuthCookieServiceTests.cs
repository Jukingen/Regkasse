using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AuthCookieServiceTests
{
    [Fact]
    public void GetCookieName_splits_admin_and_pos()
    {
        Assert.Equal("rk_admin_access_token", AuthCookieService.GetAccessCookieName("admin"));
        Assert.Equal("rk_pos_access_token", AuthCookieService.GetAccessCookieName("pos"));
        Assert.Equal("rk_admin_refresh_token", AuthCookieService.GetRefreshCookieName("admin"));
        Assert.Equal("rk_pos_refresh_token", AuthCookieService.GetRefreshCookieName("pos"));
    }

    [Fact]
    public void AppendAuthCookies_writes_admin_httponly_pair()
    {
        var http = new DefaultHttpContext();
        var service = CreateService(new AuthCookieOptions
        {
            Enabled = true,
            SameSite = "Lax",
            Secure = false,
        });

        var accessExpires = DateTime.UtcNow.AddHours(1);
        var refreshExpires = DateTime.UtcNow.AddDays(14);
        service.AppendAuthCookies(
            http.Response,
            "access.jwt.token",
            "refresh-secret",
            accessExpires,
            refreshExpires,
            clientApp: "admin");

        var setCookie = http.Response.Headers.SetCookie.ToString();
        Assert.Contains("rk_admin_access_token=access.jwt.token", setCookie, StringComparison.Ordinal);
        Assert.Contains("rk_admin_refresh_token=refresh-secret", setCookie, StringComparison.Ordinal);
        Assert.DoesNotContain("rk_pos_access_token=", setCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("access.jwt.token; expires", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppendAuthCookies_writes_pos_pair_without_overwriting_admin_names()
    {
        var http = new DefaultHttpContext();
        var service = CreateService(new AuthCookieOptions
        {
            Enabled = true,
            SameSite = "Lax",
            Secure = false,
        });

        service.AppendAuthCookies(
            http.Response,
            "pos.jwt.token",
            "pos-refresh",
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddDays(14),
            clientApp: "pos");

        var setCookie = http.Response.Headers.SetCookie.ToString();
        Assert.Contains("rk_pos_access_token=pos.jwt.token", setCookie, StringComparison.Ordinal);
        Assert.Contains("rk_pos_refresh_token=pos-refresh", setCookie, StringComparison.Ordinal);
        Assert.DoesNotContain("rk_admin_access_token=", setCookie, StringComparison.Ordinal);
    }

    [Fact]
    public void SameSite_None_forces_Secure()
    {
        var service = CreateService(new AuthCookieOptions
        {
            Enabled = true,
            SameSite = "None",
            Secure = false,
        });

        var options = service.BuildCookieOptions(
            new AuthCookieOptions { SameSite = "None", Secure = false },
            TimeSpan.FromHours(1),
            httpOnly: true);

        Assert.Equal(SameSiteMode.None, options.SameSite);
        Assert.True(options.Secure);
        Assert.True(options.HttpOnly);
    }

    [Fact]
    public void ReadAccessToken_returns_legacy_cookie_value()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = "access_token=from-cookie; refresh_token=r1";
        var service = CreateService(new AuthCookieOptions { Enabled = true });

        Assert.Equal("from-cookie", service.ReadAccessToken(http.Request));
        Assert.Equal("r1", service.ReadRefreshToken(http.Request));
    }

    [Fact]
    public void ReadAccessToken_prefers_admin_when_both_present()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie =
            "rk_admin_access_token=admin-jwt; rk_pos_access_token=pos-jwt; rk_admin_refresh_token=admin-r; rk_pos_refresh_token=pos-r";
        var service = CreateService(new AuthCookieOptions { Enabled = true });

        Assert.Equal("admin-jwt", service.ReadAccessToken(http.Request));
        Assert.Equal("admin-r", service.ReadRefreshToken(http.Request));
    }

    [Fact]
    public void ReadAccessToken_uses_pos_when_X_App_Context_is_pos()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie =
            "rk_admin_access_token=admin-jwt; rk_pos_access_token=pos-jwt; rk_pos_refresh_token=pos-r";
        http.Request.Headers["X-App-Context"] = "pos";
        var service = CreateService(new AuthCookieOptions { Enabled = true });

        Assert.Equal("pos-jwt", service.ReadAccessToken(http.Request));
        Assert.Equal("pos-r", service.ReadRefreshToken(http.Request));
    }

    [Fact]
    public void ReadAccessToken_uses_admin_cookie_for_admin_clientApp()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie =
            "rk_admin_access_token=admin-jwt; rk_pos_access_token=pos-jwt";
        var service = CreateService(new AuthCookieOptions { Enabled = true });

        Assert.Equal("admin-jwt", service.ReadAccessToken(http.Request, "admin"));
        Assert.Equal("pos-jwt", service.ReadAccessToken(http.Request, "pos"));
    }

    [Fact]
    public void ClearAuthCookies_admin_does_not_expire_pos()
    {
        var http = new DefaultHttpContext();
        var service = CreateService(new AuthCookieOptions { Enabled = true, Secure = false });
        service.ClearAuthCookies(http.Response, clientApp: "admin");

        var setCookie = http.Response.Headers.SetCookie.ToString();
        Assert.Contains("rk_admin_access_token=", setCookie, StringComparison.Ordinal);
        Assert.Contains("rk_admin_refresh_token=", setCookie, StringComparison.Ordinal);
        Assert.Contains("access_token=", setCookie, StringComparison.Ordinal);
        Assert.DoesNotContain("rk_pos_access_token=", setCookie, StringComparison.Ordinal);
        Assert.Contains("max-age=0", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClearAuthCookies_unscoped_expires_both_apps_and_legacy()
    {
        var http = new DefaultHttpContext();
        var service = CreateService(new AuthCookieOptions { Enabled = true, Secure = false });
        service.ClearAuthCookies(http.Response);

        var setCookie = http.Response.Headers.SetCookie.ToString();
        Assert.Contains("rk_admin_access_token=", setCookie, StringComparison.Ordinal);
        Assert.Contains("rk_pos_access_token=", setCookie, StringComparison.Ordinal);
        Assert.Contains("access_token=", setCookie, StringComparison.Ordinal);
        Assert.Contains("refresh_token=", setCookie, StringComparison.Ordinal);
        Assert.Contains("max-age=0", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Disabled_does_not_write_cookies()
    {
        var http = new DefaultHttpContext();
        var service = CreateService(new AuthCookieOptions { Enabled = false });
        service.AppendAuthCookies(
            http.Response,
            "tok",
            "ref",
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddDays(1),
            clientApp: "admin");
        Assert.Equal(0, http.Response.Headers.SetCookie.Count);
    }

    private static AuthCookieService CreateService(AuthCookieOptions options)
    {
        var monitor = new Mock<IOptionsMonitor<AuthCookieOptions>>();
        monitor.SetupGet(m => m.CurrentValue).Returns(options);
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);
        return new AuthCookieService(monitor.Object, env.Object);
    }
}
