namespace KasseAPI_Final.Services.Auth;

/// <summary>
/// Short-lived, single-use pending tokens issued after SuperAdmin password login
/// when production 2FA is required (skipped in Development).
/// </summary>
public interface ITwoFactorChallengeService
{
    string CreateChallenge(TwoFactorChallengePayload payload);

    /// <summary>Returns the payload and removes the challenge (one-time use).</summary>
    bool TryConsumeChallenge(string token, out TwoFactorChallengePayload? payload);
}

public sealed record TwoFactorChallengePayload(
    string UserId,
    string? ClientApp,
    string LoginIdentifier,
    bool SetupRequired,
    DateTime ExpiresAtUtc);
