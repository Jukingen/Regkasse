using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Automated TSE disaster-recovery runbooks and safe (simulation) drills — does not perform live fiscal failover by default.
/// </summary>
public interface ITseDisasterRecoveryService
{
    Task<TseDrRunbookDto> GenerateRunbookAsync(
        Guid tenantId,
        string scenario = "TSEFailure",
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TseDrExecutionResultDto> ExecuteRunbookAsync(
        Guid runbookId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TseDrStatusDto> GetDrStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<TseDrReportDto> RunDrDrillAsync(
        Guid tenantId,
        string scenario = "TSEFailure",
        string? actorUserId = null,
        CancellationToken cancellationToken = default);
}
