using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Rule-based TSE auto-healing: re-probe health, clear transient errors, optional failover.
/// Does not rewrite fiscal signature chains or certificates.
/// </summary>
public interface ITseAutoHealingService
{
    Task<TseHealingResultDto> DiagnoseAndHealAsync(
        Guid deviceId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TseHealingReportDto> GetHealingHistoryAsync(
        Guid tenantId,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<TseHealingConfigurationDto> GetHealingConfigurationAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<TseHealingConfigurationDto> ConfigureHealingAsync(
        Guid tenantId,
        ConfigureTseHealingRequestDto config,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);
}
