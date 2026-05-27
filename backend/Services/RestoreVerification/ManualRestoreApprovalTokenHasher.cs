namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>6-digit approval tokens hashed with BCrypt for manual restore second-admin flow.</summary>
public static class ManualRestoreApprovalTokenHasher
{
    /// <summary>Generates a 6-digit numeric token (100000–999999).</summary>
    public static string GenerateSixDigitToken() =>
        Random.Shared.Next(100_000, 1_000_000).ToString();

    public static string Hash(string plainToken) =>
        BCrypt.Net.BCrypt.HashPassword(Normalize(plainToken));

    public static bool Verify(string plainToken, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(Normalize(plainToken), storedHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

    public static bool IsValidSixDigitFormat(string? plainToken)
    {
        if (string.IsNullOrWhiteSpace(plainToken))
            return false;
        var t = plainToken.Trim();
        return t.Length == 6 && t.All(char.IsDigit) && int.TryParse(t, out var n) && n is >= 100_000 and <= 999_999;
    }

    private static string Normalize(string plainToken) => plainToken.Trim();
}
