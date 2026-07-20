using System.Linq;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.TwoFactor;

/// <summary>
/// Identity authenticator TOTP plus Development-only bypass codes
/// (<c>DEV-2FA-BYPASS</c> / configured <see cref="TwoFactorAuthOptions.TestToken"/>).
/// </summary>
public sealed class TwoFactorService : ITwoFactorService
{
    private readonly IHostEnvironment _environment;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOptionsMonitor<TwoFactorAuthOptions> _options;
    private readonly ILogger<TwoFactorService> _logger;

    public TwoFactorService(
        IHostEnvironment environment,
        UserManager<ApplicationUser> userManager,
        IOptionsMonitor<TwoFactorAuthOptions> options,
        ILogger<TwoFactorService> logger)
    {
        _environment = environment;
        _userManager = userManager;
        _options = options;
        _logger = logger;
    }

    public bool IsDevelopment => _environment.IsDevelopment();

    /// <inheritdoc />
    public bool IsBypassActive =>
        _environment.IsDevelopment() && _options.CurrentValue.BypassInDevelopment;

    /// <inheritdoc />
    public string? GenerateTwoFactorToken(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!_environment.IsDevelopment())
            return null;

        _logger.LogDebug(
            "Development 2FA bypass token issued for user {UserId}",
            user.Id);
        return ITwoFactorService.DevelopmentBypassToken;
    }

    /// <inheritdoc />
    public async Task<bool> VerifyTwoFactorTokenAsync(
        ApplicationUser user,
        string token,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        _ = cancellationToken;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        var trimmed = token.Trim();
        var opts = _options.CurrentValue;

        // Fail-closed: bypass codes only in Development.
        if (_environment.IsDevelopment())
        {
            if (string.Equals(trimmed, ITwoFactorService.DevelopmentBypassToken, StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "Development 2FA bypass accepted for user {UserId}",
                    user.Id);
                return true;
            }

            var testToken = string.IsNullOrWhiteSpace(opts.TestToken)
                ? ITwoFactorService.DevelopmentBypassNumericCode
                : opts.TestToken.Trim();
            if (string.Equals(trimmed, testToken, StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "Development 2FA test token accepted for user {UserId}",
                    user.Id);
                return true;
            }
        }

        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length < 6)
            return false;

        return await _userManager
            .VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, digits)
            .ConfigureAwait(false);
    }
}
