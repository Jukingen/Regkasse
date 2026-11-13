using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Development-only TSE QA simulator. Mutates <see cref="TseDevice"/> health/lifecycle fields
/// and optional probe latency — never writes PaymentDetails or signature chain rows.
/// </summary>
public interface ITseSimulatorService
{
    Task<TseSimulationResultDto> SimulateTseFailureAsync(
        Guid deviceId,
        TseSimulatorFailureType type,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TseSimulationResultDto> SimulateNetworkLatencyAsync(
        Guid deviceId,
        int latencyMs,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TseSimulationResultDto> SimulateCertificateExpiryAsync(
        Guid deviceId,
        DateTime expiryDateUtc,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TseSimulationResultDto> ResetSimulationAsync(
        Guid deviceId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TseSimulationScenarioDto>> GetAvailableScenariosAsync(
        CancellationToken cancellationToken = default);

    Task<TseSimulationDeviceSnapshotDto?> GetSnapshotAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);
}
