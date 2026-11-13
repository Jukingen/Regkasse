namespace KasseAPI_Final.Tse.Fiskaly;

public interface IFiskalyConnectionProbe
{
    Task<FiskalyConnectionProbeResult> ProbeAsync(
        FiskalyConnectionProbeRequest request,
        CancellationToken cancellationToken = default);
}
