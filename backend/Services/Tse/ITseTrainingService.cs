using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// TSE training catalog + progress and Development-only failure drills (wraps simulator).
/// Does not touch fiscal signature chains.
/// </summary>
public interface ITseTrainingService
{
    Task<TseTrainingEnvironmentDto> GetEnvironmentAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TseTrainingModuleDto>> GetModulesAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<TseTrainingModuleDto> StartModuleAsync(
        string userId,
        string moduleId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TseTrainingConsoleEntryDto>> GetConsoleAsync(
        string userId,
        int take = 100,
        CancellationToken cancellationToken = default);

    void ClearConsole(string userId);

    Task<TseTrainingSimulateResultDto> SimulateFailureAsync(
        string userId,
        Guid deviceId,
        string failureType,
        CancellationToken cancellationToken = default);

    Task<TseTrainingSimulateResultDto> ResetSimulationAsync(
        string userId,
        Guid deviceId,
        CancellationToken cancellationToken = default);
}
