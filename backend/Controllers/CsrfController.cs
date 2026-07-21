using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Issues a CSRF token for double-submit protection (cookie + response body for the header).
/// </summary>
[ApiController]
[Route("api/csrf")]
[AllowAnonymous]
public sealed class CsrfController : ControllerBase
{
    private readonly ICsrfTokenService _csrfTokenService;
    private readonly IOptionsMonitor<CsrfOptions> _options;
    private readonly IHostEnvironment _environment;

    public CsrfController(
        ICsrfTokenService csrfTokenService,
        IOptionsMonitor<CsrfOptions> options,
        IHostEnvironment environment)
    {
        _csrfTokenService = csrfTokenService;
        _options = options;
        _environment = environment;
    }

    /// <summary>
    /// Returns a new CSRF token and sets the matching cookie (readable by JS for double-submit).
    /// Clients must send the token in <c>X-XSRF-TOKEN</c> (or configured header) on mutations.
    /// </summary>
    [HttpGet("token")]
    [ProducesResponseType(typeof(CsrfTokenResponse), StatusCodes.Status200OK)]
    public ActionResult<CsrfTokenResponse> GetToken()
    {
        var options = _options.CurrentValue;
        var headerName = string.IsNullOrWhiteSpace(options.HeaderName)
            ? "X-XSRF-TOKEN"
            : options.HeaderName.Trim();
        var cookieName = string.IsNullOrWhiteSpace(options.CookieName)
            ? "XSRF-TOKEN"
            : options.CookieName.Trim();
        var hours = Math.Clamp(options.TokenLifetimeHours, 1, 168);

        var token = _csrfTokenService.GenerateToken();
        var isDev = _environment.IsDevelopment();

        Response.Cookies.Append(
            cookieName,
            token,
            new CookieOptions
            {
                // Not HttpOnly: FA reads XSRF-TOKEN via document.cookie and mirrors it to X-XSRF-TOKEN.
                HttpOnly = false,
                // SameSite=None requires Secure; use Lax + insecure on local http.
                Secure = !isDev,
                SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None,
                IsEssential = true,
                MaxAge = TimeSpan.FromHours(hours),
                Path = "/",
            });

        return Ok(new CsrfTokenResponse
        {
            Token = token,
            HeaderName = headerName,
            CookieName = cookieName,
            ExpiresInHours = hours,
        });
    }
}

public sealed class CsrfTokenResponse
{
    public string Token { get; set; } = string.Empty;
    public string HeaderName { get; set; } = "X-XSRF-TOKEN";
    public string CookieName { get; set; } = "XSRF-TOKEN";
    public int ExpiresInHours { get; set; }
}
