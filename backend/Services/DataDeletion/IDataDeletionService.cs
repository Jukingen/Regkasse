using KasseAPI_Final.Services.DataExport;

namespace KasseAPI_Final.Services.DataDeletion;

public interface IDataDeletionService
{
    /// <summary>
    /// Creates a deletion request for an Archived tenant (license overdue &gt; 30 days).
    /// Sends notification to Mandanten-Admin with Super Admin CC.
    /// </summary>
    Task<TenantDataDeletionRequestDto> RequestDeletionAsync(
        Guid tenantId,
        string? requestedByUserId,
        string? reason,
        CancellationToken ct = default);

    /// <summary>FA confirmation button — starts the 7-day wait before purge.</summary>
    Task<TenantDataDeletionRequestDto> ConfirmDeletionAsync(
        Guid tenantId,
        Guid requestId,
        string? confirmedByUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Executes irreversible non-RKSV purge after the 7-day wait.
    /// Used by Super Admin manual execute and the auto-purge hosted service.
    /// </summary>
    Task<DeletionResult> ExecutePurgeAsync(
        Guid requestId,
        string? actorUserId,
        string executedVia,
        CancellationToken ct = default);

    /// <summary>Requests that are confirmed and past the 7-day wait (for auto-purge).</summary>
    Task<IReadOnlyList<Guid>> ListPurgeEligibleRequestIdsAsync(CancellationToken ct = default);
}
