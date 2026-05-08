using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Regkasse.LicenseTools;

/// <summary>
/// Builds REGK-XXXXX-XXXXX-XXXXX (derived fingerprint) and RS256 JWT bound to that key.
/// Cryptographic proof lives in the JWT; the REGK segments encode a deterministic digest of payload|signature.
/// </summary>
public static class LicenseIssuer
{
    private const ulong SegmentMod = 36UL * 36 * 36 * 36 * 36; // 36^5
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <param name="customerName">Displayed customer; pipe characters are stripped.</param>
    /// <param name="machineHashHex">SHA-256 hex from POS machine, or null/empty for floating license.</param>
    /// <param name="expiresAtUtc">JWT exp (and signed payload date) — typically end-of-day UTC for the chosen calendar date.</param>
    /// <param name="privateKey">RSA private key used to sign the JWT (PKCS#1 or PKCS#8 PEM).</param>
    public static LicenseIssueResult Issue(
        string customerName,
        string? machineHashHex,
        DateTimeOffset expiresAtUtc,
        RSA privateKey)
    {
        ArgumentNullException.ThrowIfNull(privateKey);

        var customer = SanitizeCustomer(customerName);
        var machine = string.IsNullOrWhiteSpace(machineHashHex)
            ? ""
            : machineHashHex.Trim().ToLowerInvariant();

        var salt = Guid.NewGuid().ToString("N");
        var expiryDate = expiresAtUtc.UtcDateTime.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var canonicalPayload = string.Join('|', customer, machine, expiryDate, salt);

        var payloadBytes = Encoding.UTF8.GetBytes(canonicalPayload);
        var signature = privateKey.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var combined = new byte[payloadBytes.Length + signature.Length];
        payloadBytes.AsSpan().CopyTo(combined);
        signature.AsSpan().CopyTo(combined.AsSpan(payloadBytes.Length));
        var digest = SHA256.HashData(combined);
        var licenseKey = "REGK-" + EncodeSegment(digest.AsSpan(0, 6)) + "-" +
                         EncodeSegment(digest.AsSpan(6, 6)) + "-" +
                         EncodeSegment(digest.AsSpan(12, 6));

        var expUnix = expiresAtUtc.ToUnixTimeSeconds();
        var jwt = CreateRs256Jwt(
            privateKey,
            licenseKey,
            machine,
            customer,
            expUnix);

        var publicPem = privateKey.ExportRSAPublicKeyPem();

        return new LicenseIssueResult(licenseKey, jwt, canonicalPayload, publicPem, expiresAtUtc);
    }

    /// <summary>Writes a new RSA key pair to PEM files (internal tooling).</summary>
    public static void GenerateKeyPair(string privateKeyPath, string publicKeyPath, int keySize = 3072)
    {
        using var rsa = RSA.Create(keySize);
        var privPem = rsa.ExportRSAPrivateKeyPem();
        var pubPem = rsa.ExportRSAPublicKeyPem();
        File.WriteAllText(privateKeyPath, privPem, Encoding.UTF8);
        File.WriteAllText(publicKeyPath, pubPem, Encoding.UTF8);
    }

    private static string SanitizeCustomer(string customerName)
    {
        var s = (customerName ?? "").Replace("|", " ", StringComparison.Ordinal).Trim();
        if (s.Length == 0)
            throw new ArgumentException("Customer name is required.", nameof(customerName));
        return s;
    }

    private static string EncodeSegment(ReadOnlySpan<byte> sixBytes)
    {
        if (sixBytes.Length != 6)
            throw new ArgumentException("Expected 6 bytes.", nameof(sixBytes));

        ulong v = 0;
        for (var i = 0; i < 6; i++)
            v = (v << 8) | sixBytes[i];

        v %= SegmentMod;
        Span<char> chars = stackalloc char[5];
        for (var i = 4; i >= 0; i--)
        {
            var idx = (int)(v % 36);
            chars[i] = Alphabet[idx];
            v /= 36;
        }

        return new string(chars);
    }

    private static string CreateRs256Jwt(
        RSA privateKey,
        string licenseKey,
        string machineHashHex,
        string customer,
        long expUnixSeconds)
    {
        var headerObj = new { alg = "RS256", typ = "JWT" };
        var payloadObj = new
        {
            licenseKey,
            machineHash = machineHashHex,
            customer,
            exp = expUnixSeconds,
        };

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var headerJson = JsonSerializer.Serialize(headerObj, jsonOptions);
        var payloadJson = JsonSerializer.Serialize(payloadObj, jsonOptions);

        var headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var signingInput = Encoding.ASCII.GetBytes(headerB64 + "." + payloadB64);
        var sig = privateKey.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var sigB64 = Base64UrlEncode(sig);
        return headerB64 + "." + payloadB64 + "." + sigB64;
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal);
    }
}
