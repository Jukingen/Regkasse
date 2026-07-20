namespace KasseAPI_Final.Services.DataDeletion;

public sealed class DeletionResult
{
    public bool Succeeded { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }
    public Guid? RequestId { get; init; }
    public Guid? TenantId { get; init; }
    public IReadOnlyDictionary<string, int>? DeletedCounts { get; init; }

    public static DeletionResult Success(
        Guid requestId,
        Guid tenantId,
        IReadOnlyDictionary<string, int>? deletedCounts = null) =>
        new()
        {
            Succeeded = true,
            RequestId = requestId,
            TenantId = tenantId,
            DeletedCounts = deletedCounts,
        };

    public static DeletionResult Fail(string error, string? code = null) =>
        new()
        {
            Succeeded = false,
            Error = error,
            ErrorCode = code,
        };
}

public static class DataDeletionErrorCodes
{
    public const string NotFound = "not_found";
    public const string NotArchived = "not_archived";
    public const string AlreadyPurged = "already_purged";
    public const string NotConfirmed = "not_confirmed";
    public const string GracePeriodActive = "grace_period_active";
    public const string InvalidStatus = "invalid_status";
    public const string AlreadyCompleted = "already_completed";
}
