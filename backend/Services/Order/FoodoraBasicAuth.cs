using System.Security.Cryptography;
using System.Text;

namespace KasseAPI_Final.Services.Order;

/// <summary>
/// Foodora webhook check. The Vendor Portal static token is sent as
/// <c>Authorization: Basic &lt;base64(user:password)&gt;</c>. Comparison is fixed-time.
/// </summary>
public static class FoodoraBasicAuth
{
    public static bool Matches(string? authorizationHeader, string? webhookToken)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader) || string.IsNullOrWhiteSpace(webhookToken))
            return false;

        var header = authorizationHeader.Trim();
        var token = webhookToken.Trim();
        if (FixedEquals(header, token))
            return true;

        const string prefix = "Basic ";
        if (header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var payload = header[prefix.Length..].Trim();
            if (FixedEquals(payload, token))
                return true;

            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                if (FixedEquals(decoded, token))
                    return true;
            }
            catch (FormatException)
            {
                // Not base64. The other comparisons already ran.
            }
        }

        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(token));
        return FixedEquals(header, prefix + encoded);
    }

    private static bool FixedEquals(string left, string right)
    {
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(left));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(right));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
