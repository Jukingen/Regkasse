using System.Security.Cryptography;

namespace KasseAPI_Final.Helpers;

/// <summary>Generates quick-tenant-user email addresses: {role}_{random6}@{slug}.regkasse.at</summary>
public static class QuickUserEmailGenerator
{
    private const string AlphanumericLower = "abcdefghijklmnopqrstuvwxyz0123456789";
    private const int SuffixLength = 6;

    public static string BuildEmail(string role, string tenantSlug)
    {
        var rolePart = role.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(rolePart))
            rolePart = "user";

        var slug = tenantSlug.Trim().ToLowerInvariant();
        return $"{rolePart}_{GenerateSuffix()}@{slug}.regkasse.at";
    }

    public static string GenerateSuffix()
    {
        Span<char> buffer = stackalloc char[SuffixLength];
        for (var i = 0; i < SuffixLength; i++)
            buffer[i] = AlphanumericLower[RandomNumberGenerator.GetInt32(AlphanumericLower.Length)];

        return new string(buffer);
    }
}
