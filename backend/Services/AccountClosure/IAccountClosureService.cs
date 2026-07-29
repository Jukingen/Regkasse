using KasseAPI_Final.Models;
using KasseAPI_Final.Services.DataDeletion;
using KasseAPI_Final.Services.DataExport;

namespace KasseAPI_Final.Services.AccountClosure;

/// <summary>
/// Account closure for non-renewing mandants (Archived license).
/// Thin facade over <see cref="IDataDeletionService"/> — no parallel entity/table.
/// Purge wait is <see cref="DataDeletionService.ConfirmationWaitDays"/> after FA confirmation (not 30 days).
/// </summary>
public interface IAccountClosureService
{
    Task<ClosureResult> RequestClosureAsync(
        Guid tenantId,
        string? reason,
        string? requestedByUserId = null,
        CancellationToken ct = default);

    Task<ClosureResult> GetClosureStatusAsync(Guid tenantId, CancellationToken ct = default);

    Task<ClosureResult> CancelClosureAsync(
        Guid tenantId,
        string? cancelledByUserId = null,
        CancellationToken ct = default);

    Task<ClosureResult> ConfirmClosureAsync(
        Guid tenantId,
        Guid closureId,
        string? confirmedByUserId = null,
        CancellationToken ct = default);

    /// <summary>Executes irreversible non-RKSV purge after the confirmation wait (Super Admin / auto-purge).</summary>
    Task<ClosureResult> ExecuteClosureAsync(
        Guid closureId,
        string? actorUserId = null,
        string executedVia = TenantDataDeletionExecutedVia.Manual,
        CancellationToken ct = default);
}

public sealed class ClosureResult
{
    public bool Succeeded { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }
    public Guid? ClosureId { get; init; }
    public Guid? TenantId { get; init; }
    public string? Status { get; init; }
    public string? Reason { get; init; }
    public DateTime? RequestedAtUtc { get; init; }
    public DateTime? ConfirmedAtUtc { get; init; }
    public DateTime? ScheduledPurgeAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public bool HasRksvData { get; init; }
    public int ConfirmationWaitDays { get; init; } = DataDeletionService.ConfirmationWaitDays;
    public IReadOnlyDictionary<string, int>? DeletedCounts { get; init; }

    public static ClosureResult Fail(string error, string? code = null) =>
        new()
        {
            Succeeded = false,
            Error = error,
            ErrorCode = code,
        };

    public static ClosureResult FromDeletion(
        TenantDataDeletionRequestDto deletion,
        Guid tenantId,
        bool hasRksvData,
        IReadOnlyDictionary<string, int>? deletedCounts = null) =>
        new()
        {
            Succeeded = true,
            ClosureId = deletion.Id,
            TenantId = tenantId,
            Status = deletion.Status,
            Reason = deletion.Reason,
            RequestedAtUtc = deletion.RequestedAtUtc,
            ConfirmedAtUtc = deletion.ConfirmedAtUtc,
            ScheduledPurgeAtUtc = deletion.PurgeEligibleAtUtc,
            CompletedAtUtc = deletion.CompletedAtUtc,
            HasRksvData = hasRksvData,
            ConfirmationWaitDays = deletion.ConfirmationWaitDays,
            DeletedCounts = deletedCounts,
        };
}
