using System.Net.Http.Headers;
using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// S3-compatible PUT/GET/HEAD with SigV4. Used for AWS Glacier/Deep Archive and GCS HMAC interop.
/// </summary>
public sealed class S3CompatibleCloudStorageProvider : ICloudStorageProvider
{
    private readonly HttpClient _http;
    private readonly CloudStorageProviderKind _kind;
    private readonly Func<(string? Bucket, string? Region, string? AccessKey, string? Secret, string? Endpoint, string StorageClass, bool ObjectLock)> _resolve;

    public S3CompatibleCloudStorageProvider(
        HttpClient http,
        CloudStorageProviderKind kind,
        Func<(string? Bucket, string? Region, string? AccessKey, string? Secret, string? Endpoint, string StorageClass, bool ObjectLock)> resolve)
    {
        _http = http;
        _kind = kind;
        _resolve = resolve;
    }

    public CloudStorageProviderKind Kind => _kind;

    public bool IsConfigured
    {
        get
        {
            var c = _resolve();
            return !string.IsNullOrWhiteSpace(c.Bucket)
                   && !string.IsNullOrWhiteSpace(c.AccessKey)
                   && !string.IsNullOrWhiteSpace(c.Secret);
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
                Error = $"{Kind} is not configured (bucket / access key / secret)."
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

        var c = _resolve();
        var uri = BuildObjectUri(c.Endpoint, c.Bucket!, c.Region, request.ObjectKey);
        using var msg = new HttpRequestMessage(HttpMethod.Put, uri);
        msg.Content = new ByteArrayContent(request.Content);
        msg.Content.Headers.ContentType = new MediaTypeHeaderValue(request.ContentType ?? "application/octet-stream");
        msg.Headers.TryAddWithoutValidation("x-amz-storage-class", c.StorageClass);
        if (c.ObjectLock && request.ImmutableUntilUtc is DateTime until)
        {
            msg.Headers.TryAddWithoutValidation("x-amz-object-lock-mode", "COMPLIANCE");
            msg.Headers.TryAddWithoutValidation(
                "x-amz-object-lock-retain-until-date",
                until.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        }

        AwsSigV4Signer.Sign(
            msg,
            c.AccessKey!,
            c.Secret!,
            string.IsNullOrWhiteSpace(c.Region) ? "us-east-1" : c.Region!,
            "s3",
            request.Content,
            DateTime.UtcNow);

        using var resp = await _http.SendAsync(msg, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new CloudStorageUploadResult
            {
                Success = false,
                Provider = Kind,
                Error = $"Cloud PUT failed: {(int)resp.StatusCode} {Trim(body)}"
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
        var c = RequireConfigured();
        var uri = BuildObjectUri(c.Endpoint, c.Bucket!, c.Region, objectKey);
        using var msg = new HttpRequestMessage(HttpMethod.Get, uri);
        AwsSigV4Signer.Sign(msg, c.AccessKey!, c.Secret!, c.Region ?? "us-east-1", "s3", null, DateTime.UtcNow);
        var resp = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            resp.Dispose();
            throw new FileNotFoundException($"Cloud GET failed: {(int)resp.StatusCode}");
        }

        return await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return false;

        var c = _resolve();
        var uri = BuildObjectUri(c.Endpoint, c.Bucket!, c.Region, objectKey);
        using var msg = new HttpRequestMessage(HttpMethod.Head, uri);
        AwsSigV4Signer.Sign(msg, c.AccessKey!, c.Secret!, c.Region ?? "us-east-1", "s3", null, DateTime.UtcNow);
        using var resp = await _http.SendAsync(msg, cancellationToken).ConfigureAwait(false);
        return resp.IsSuccessStatusCode;
    }

    private (string? Bucket, string? Region, string? AccessKey, string? Secret, string? Endpoint, string StorageClass, bool ObjectLock) RequireConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException($"{Kind} is not configured.");
        return _resolve();
    }

    internal static Uri BuildObjectUri(string? endpoint, string bucket, string? region, string objectKey)
    {
        var key = objectKey.Replace('\\', '/').TrimStart('/');
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            var baseUri = endpoint.TrimEnd('/');
            return new Uri($"{baseUri}/{Uri.EscapeDataString(bucket)}/{EscapeKey(key)}", UriKind.Absolute);
        }

        var hostRegion = string.IsNullOrWhiteSpace(region) ? "us-east-1" : region;
        return new Uri($"https://{bucket}.s3.{hostRegion}.amazonaws.com/{EscapeKey(key)}", UriKind.Absolute);
    }

    private static string EscapeKey(string key) =>
        string.Join('/', key.Split('/').Select(Uri.EscapeDataString));

    private string Redact(string objectKey)
    {
        var bucket = _resolve().Bucket ?? "bucket";
        return $"{Kind.ToString().ToLowerInvariant()}://{bucket}/{objectKey.Replace('\\', '/').TrimStart('/')}";
    }

    private static string Trim(string body) =>
        body.Length <= 240 ? body : body[..240];
}
