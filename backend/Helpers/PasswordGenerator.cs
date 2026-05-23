using System.Security.Cryptography;

namespace KasseAPI_Final.Helpers;

/// <summary>Generates compliant random passwords for operator handoff (shown once).</summary>
public static class PasswordGenerator
{
    private const string Lowers = "abcdefghjkmnpqrstuvwxyz";
    private const string Uppers = "ABCDEFGHJKMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%^&*()";
    private const int MinLength = 12;

    /// <summary>
    /// At least 12 characters with upper, lower, digit, and special character.
    /// </summary>
    public static string GenerateRandomPassword(int length = 14)
    {
        return GenerateSecurePassword(length);
    }

    /// <summary>
    /// Cryptographically secure password with at least one upper, lower, digit, and special (default 12 chars).
    /// </summary>
    public static string GenerateSecurePassword(int length = 12)
    {
        if (length < MinLength)
            length = MinLength;

        var all = Lowers + Uppers + Digits + Symbols;
        Span<char> buffer = stackalloc char[length];
        buffer[0] = Lowers[RandomNumberGenerator.GetInt32(Lowers.Length)];
        buffer[1] = Uppers[RandomNumberGenerator.GetInt32(Uppers.Length)];
        buffer[2] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        buffer[3] = Symbols[RandomNumberGenerator.GetInt32(Symbols.Length)];
        for (var i = 4; i < buffer.Length; i++)
            buffer[i] = all[RandomNumberGenerator.GetInt32(all.Length)];

        for (var i = buffer.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
        }

        return new string(buffer);
    }
}
