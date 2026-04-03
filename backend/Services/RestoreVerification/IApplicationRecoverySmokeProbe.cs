namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Kurtarma sonrası uygulama HTTP uç noktası duman testi (ayrı dağıtım URL’si; bu API süreci ile aynı olmak zorunda değil).
/// </summary>
public interface IApplicationRecoverySmokeProbe
{
    Task<ApplicationSmokeProbeOutcome> ProbeAsync(Uri baseUri, string relativePath, TimeSpan timeout, CancellationToken ct);
}
