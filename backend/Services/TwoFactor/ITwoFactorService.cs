using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.TwoFactor;

/// <summary>
/// SuperAdmin TOTP verification. In Development, fixed bypass codes are accepted
/// so local login does not require an authenticator app (when bypass is configured).
/// </summary>
public interface ITwoFactorService
{
    /// <summary>Development bypass token returned by <see cref="GenerateTwoFactorToken"/>.</summary>
    const string DevelopmentBypassToken = "DEV-2FA-BYPASS";

    /// <summary>Default numeric Development bypass when <c>TwoFactorAuth:TestToken</c> is empty.</summary>
    const string DevelopmentBypassNumericCode = "123456";

    bool IsDevelopment { get; }

    /// <summary>
    /// True when the API is Development and <c>TwoFactorAuth:BypassInDevelopment</c> is enabled
    /// (login skips the challenge).
    /// </summary>
    bool IsBypassActive { get; }

    /// <summary>
    /// In Development returns <see cref="DevelopmentBypassToken"/>.
    /// In Production returns null — authenticator apps generate TOTP codes; the API does not emit them.
    /// </summary>
    string? GenerateTwoFactorToken(ApplicationUser user);

    /// <summary>
    /// Verifies a TOTP (or Development bypass / test token) code for the user.
    /// </summary>
    Task<bool> VerifyTwoFactorTokenAsync(
        ApplicationUser user,
        string token,
        CancellationToken cancellationToken = default);
}
