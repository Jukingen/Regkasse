namespace KasseAPI_Final.Services.Countries.KassenSicherheit;

/// <summary>
/// Non-success SIGN DE or DSFinV-K HTTP response. The response body is not stored or logged.
/// </summary>
public sealed class KassenSicherheitHttpException : Exception
{
    public KassenSicherheitHttpException(int statusCode, string? providerErrorCode)
        : base("SIGN DE HTTP failed with status " + statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".")
    {
        StatusCode = statusCode;
        ProviderErrorCode = providerErrorCode;
    }

    public int StatusCode { get; }

    public string? ProviderErrorCode { get; }
}
