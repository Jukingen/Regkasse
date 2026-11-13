using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Development-only TSE developer experience tools.
/// Never writes PaymentDetails, receipts, or signature-chain rows.
/// </summary>
public interface ITseDeveloperToolsService
{
    bool IsEnabled { get; }

    Task<TseDeveloperToolsAvailabilityDto> GetAvailabilityAsync(
        CancellationToken cancellationToken = default);

    Task<TseDevToolResultDto> RunDiagnosticsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds synthetic health-sample "traffic" (probe load), not fiscal payments.
    /// </summary>
    Task<TseDevToolResultDto> SimulateTrafficAsync(
        Guid tenantId,
        int transactionCount,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TseDevToolResultDto> ValidateConfigAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds non-fiscal operational test data (health samples + sample incident).
    /// </summary>
    Task<TseDevToolResultDto> GenerateTestDataAsync(
        Guid tenantId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);
}
