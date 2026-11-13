using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models.Backup;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Azure Blob PUT/GET/HEAD with Shared Key; Access Tier = Archive.</summary>
public sealed class AzureArchiveCloudStorageProvider : ICloudStorageProvider
{
    private readonly HttpClient _http;
    private readonly IOptionsMonitor<BackupOptions> _options;

    public AzureArchiveCloudStorageProvider(HttpClient http, IOptionsMonitor<BackupOptions> options)
    {
        _http = http;
        _options = options;
    }

    public CloudStorageProviderKind Kind => CloudStorageProviderKind.AzureArchive;

    public bool IsConfigured
    {
        get
        {
            var a = _options.CurrentValue.CloudStorage.Azure;
            return !string.IsNullOrWhiteSpace(a.AccountName)
                   && !string.IsNullOrWhiteSpace(a.AccountKey)
                   && !string.IsNullOrWhiteSpace(a.Container);
        }
    }

    public async Task<CloudStorageUploadResult> UploadAsync(
        CloudStorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new CloudStorageUploadResult
            {
                Success = false,
                Provider = Kind,
                Error = "Azure Archive is not configured (account / key / container)."
            };
        }

        if (await ExistsAsync(request.ObjectKey, cancellationToken).ConfigureAwait(false))
        {
            return new CloudStorageUploadResult
            {
                Success = false,
                Provider = Kind,
                Error = "WORM refuse: object already exists.",
                RedactedLocator = Redact(request.ObjectKey)
            };
        }

        var a = _options.CurrentValue.CloudStorage.Azure;
        var uri = BuildBlobUri(a.AccountName!, a.Container!, request.ObjectKey);
        using var msg = new HttpRequestMessage(HttpMethod.Put, uri);
        msg.Content = new ByteArrayContent(request.Content);
        msg.Content.Headers.ContentType = new MediaTypeHeaderValue(request.ContentType ?? "application/octet-stream");
        msg.Content.Headers.ContentLength = request.Content.LongLength;
        msg.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob");
        msg.Headers.TryAddWithoutValidation("x-ms-version", "2023-11-03");
        msg.Headers.TryAddWithoutValidation("x-ms-date", DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture));
        msg.Headers.TryAddWithoutValidation("x-ms-access-tier", string.IsNullOrWhiteSpace(a.AccessTier) ? "Archive" : a.AccessTier);
        SignSharedKey(msg, a.AccountName!, a.AccountKey!, a.Container!, request.ObjectKey);

        using var resp = await _http.SendAsync(msg, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new CloudStorageUploadResult
            {
                Success = false,
                Provider = Kind,
                Error = $"Azure PUT failed: {(int)resp.StatusCode} {Trim(body)}"
            };
        }

        return new CloudStorageUploadResult
        {
            Success = true,
            Provider = Kind,
            RedactedLocator = Redact(request.ObjectKey),
            ByteSize = request.Content.LongLength
        };
    }

    public async Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Azure Archive is not configured.");

        var a = _options.CurrentValue.CloudStorage.Azure;
        var uri = BuildBlobUri(a.AccountName!, a.Container!, objectKey);
        using var msg = new HttpRequestMessage(HttpMethod.Get, uri);
        msg.Headers.TryAddWithoutValidation("x-ms-version", "2023-11-03");
        msg.Headers.TryAddWithoutValidation("x-ms-date", DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture));
        SignSharedKey(msg, a.AccountName!, a.AccountKey!, a.Container!, objectKey);
        var resp = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            resp.Dispose();
            throw new FileNotFoundException($"Azure GET failed: {(int)resp.StatusCode}");
        }

        return await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return false;

        var a = _options.CurrentValue.CloudStorage.Azure;
        var uri = BuildBlobUri(a.AccountName!, a.Container!, objectKey);
        using var msg = new HttpRequestMessage(HttpMethod.Head, uri);
        msg.Headers.TryAddWithoutValidation("x-ms-version", "2023-11-03");
        msg.Headers.TryAddWithoutValidation("x-ms-date", DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture));
        SignSharedKey(msg, a.AccountName!, a.AccountKey!, a.Container!, objectKey);
        using var resp = await _http.SendAsync(msg, cancellationToken).ConfigureAwait(false);
        return resp.IsSuccessStatusCode;
    }

    internal static Uri BuildBlobUri(string account, string container, string objectKey)
    {
        var key = objectKey.Replace('\\', '/').TrimStart('/');
        return new Uri(
            $"https://{account}.blob.core.windows.net/{container}/{EscapeKey(key)}",
            UriKind.Absolute);
    }

    private static void SignSharedKey(
        HttpRequestMessage request,
        string account,
        string accountKey,
        string container,
        string objectKey)
    {
        var key = objectKey.Replace('\\', '/').TrimStart('/');
        var contentLength = request.Content?.Headers.ContentLength?.ToString(CultureInfo.InvariantCulture) ?? "";
        var contentType = request.Content?.Headers.ContentType?.ToString() ?? "";
        var date = request.Headers.TryGetValues("x-ms-date", out var dates)
            ? dates.First()
            : DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture);

        var canonicalHeaders = string.Join(
            '\n',
            request.Headers
                .Where(h => h.Key.StartsWith("x-ms-", StringComparison.OrdinalIgnoreCase))
                .Select(h => (Name: h.Key.ToLowerInvariant(), Value: string.Join(',', h.Value).Trim()))
                .OrderBy(h => h.Name, StringComparer.Ordinal)
                .Select(h => h.Name + ":" + h.Value));

        var canonicalResource = $"/{account}/{container}/{key}";
        var stringToSign =
            $"{request.Method.Method}\n\n\n{contentLength}\n\n{contentType}\n\n\n\n\n\n\n{canonicalHeaders}\n{canonicalResource}";

        var keyBytes = Convert.FromBase64String(accountKey);
        var hmac = HMACSHA256.HashData(keyBytes, Encoding.UTF8.GetBytes(stringToSign));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "SharedKey",
            $"{account}:{Convert.ToBase64String(hmac)}");
        _ = date;
    }

    private string Redact(string objectKey)
    {
        var a = _options.CurrentValue.CloudStorage.Azure;
        return $"azure://{a.Container}/{objectKey.Replace('\\', '/').TrimStart('/')}";
    }

    private static string EscapeKey(string key) =>
        string.Join('/', key.Split('/').Select(Uri.EscapeDataString));

    private static string Trim(string body) =>
        body.Length <= 240 ? body : body[..240];
}
