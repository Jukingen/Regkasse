namespace KasseAPI_Final.Services.Countries.KassenSicherheit;

/// <summary>
/// <c>KassenSicherheit:Provider</c> is not <c>fiskaly-de</c>. Callers must not fall back to Austrian TSE.
/// </summary>
public sealed class KassenSicherheitNotConfiguredException : InvalidOperationException
{
    public const string DefaultMessage = "KassenSicherheit provider is not configured.";

    public KassenSicherheitNotConfiguredException()
        : base(DefaultMessage)
    {
    }
}

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
