using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace KasseAPI_Final.Services.Backup;

/// <summary>AWS Signature Version 4 for S3-compatible PUT/GET/HEAD (no SDK).</summary>
public static class AwsSigV4Signer
{
    public static void Sign(
        HttpRequestMessage request,
        string accessKeyId,
        string secretAccessKey,
        string region,
        string service,
        byte[]? payload,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(request);
        var amzDate = utcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var dateStamp = utcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        request.Headers.Remove("x-amz-date");
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);

        var payloadHash = Hex(SHA256.HashData(payload ?? Array.Empty<byte>()));
        request.Headers.Remove("x-amz-content-sha256");
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);

        var host = request.RequestUri?.Host ?? throw new InvalidOperationException("Request URI host is required.");
        request.Headers.Host = host;

        var canonicalHeaders = BuildCanonicalHeaders(request, host, payloadHash, amzDate);
        var signedHeaders = string.Join(';', canonicalHeaders.Select(h => h.Name));
        var canonicalRequest = string.Join('\n', new[]
        {
            request.Method.Method,
            request.RequestUri!.AbsolutePath,
            request.RequestUri.Query.TrimStart('?'),
            string.Join('\n', canonicalHeaders.Select(h => h.Name + ":" + h.Value)) + "\n",
            signedHeaders,
            payloadHash
        });

        var credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";
        var stringToSign = string.Join('\n', new[]
        {
            "AWS4-HMAC-SHA256",
            amzDate,
            credentialScope,
            Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))
        });

        var signingKey = GetSignatureKey(secretAccessKey, dateStamp, region, service);
        var signature = Hex(HmacSha256(signingKey, stringToSign));
        request.Headers.Remove("Authorization");
        request.Headers.TryAddWithoutValidation(
            "Authorization",
            $"AWS4-HMAC-SHA256 Credential={accessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}");
    }

    internal static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    internal static byte[] HmacSha256(byte[] key, string data) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));

    internal static byte[] GetSignatureKey(string secret, string dateStamp, string region, string service)
    {
        var kDate = HmacSha256(Encoding.UTF8.GetBytes("AWS4" + secret), dateStamp);
        var kRegion = HmacSha256(kDate, region);
        var kService = HmacSha256(kRegion, service);
        return HmacSha256(kService, "aws4_request");
    }

    private static List<(string Name, string Value)> BuildCanonicalHeaders(
        HttpRequestMessage request,
        string host,
        string payloadHash,
        string amzDate)
    {
        var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
        map["host"] = host;
        map["x-amz-content-sha256"] = payloadHash;
        map["x-amz-date"] = amzDate;

        foreach (var header in request.Headers)
        {
            var name = header.Key.ToLowerInvariant();
            if (name.StartsWith("x-amz-", StringComparison.Ordinal) && name is not "x-amz-date" and not "x-amz-content-sha256")
                map[name] = string.Join(',', header.Value).Trim();
        }

        if (request.Content != null)
        {
            foreach (var header in request.Content.Headers)
            {
                var name = header.Key.ToLowerInvariant();
                if (name is "content-type" or "content-length")
                    map[name] = string.Join(',', header.Value).Trim();
            }
        }

        return map.Select(kv => (kv.Key, kv.Value)).ToList();
    }
}
