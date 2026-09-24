namespace KasseAPI_Final.Configuration;

/// <summary>
/// Hosted Peppol Access Point. <c>AccessPointMode=own</c> is rejected.
/// <c>Provider=not-configured</c> validates XML and does not send.
/// Secrets: <c>Peppol__ApiKey</c>. Do not commit certificates.
/// </summary>
public sealed class PeppolOptions
{
    public const string SectionName = "Peppol";

    public string AccessPointMode { get; set; } = "hosted";

    public string Environment { get; set; } = "TEST";

    public string Provider { get; set; } = "not-configured";

    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
}
