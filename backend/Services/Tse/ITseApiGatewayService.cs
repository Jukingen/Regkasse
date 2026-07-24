using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Operational TSE API gateway: load-balances provider health probes across configured backends.
/// Does not route fiscal Sign / Startbeleg — those stay on <c>ITseProviderFactory</c>.
/// </summary>
public interface ITseApiGatewayService
{
    Task<TseGatewayResponseDto> RouteRequestAsync(
        TseGatewayRequestDto request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TseGatewayStatusDto> GetGatewayStatusAsync(CancellationToken cancellationToken = default);

    Task<TseGatewayConfigDto> GetGatewayConfigAsync(CancellationToken cancellationToken = default);

    Task<TseGatewayConfigDto> ConfigureGatewayAsync(
        ConfigureTseGatewayRequestDto config,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);
}
