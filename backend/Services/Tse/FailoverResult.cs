namespace KasseAPI_Final.Services.Tse;

/// <summary>Outcome of an automatic or manual TSE failover / revert operation.</summary>
public sealed class FailoverResult
{
    public bool Succeeded { get; init; }

    /// <summary>Alias for <see cref="Succeeded"/> (sketch / caller compatibility).</summary>
    public bool IsSuccess => Succeeded;

    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// True when operators should intervene (e.g. no healthy backup, backup also unhealthy).
    /// </summary>
    public bool NeedsAttention { get; init; }

    public string FailoverType { get; set; } = Models.TseFailoverTypes.Automatic;

    public Guid? PrimaryDeviceId { get; init; }

    public Guid? BackupDeviceId { get; init; }

    public Guid? LogId { get; init; }

    public static FailoverResult Success(
        string message,
        Guid? primaryId = null,
        Guid? backupId = null,
        Guid? logId = null,
        string failoverType = Models.TseFailoverTypes.Automatic) =>
        new()
        {
            Succeeded = true,
            Message = message,
            FailoverType = failoverType,
            PrimaryDeviceId = primaryId,
            BackupDeviceId = backupId,
            LogId = logId,
        };

    public static FailoverResult Fail(
        string message,
        Guid? primaryId = null,
        Guid? backupId = null,
        Guid? logId = null,
        string failoverType = Models.TseFailoverTypes.Automatic,
        bool needsAttention = false) =>
        new()
        {
            Succeeded = false,
            Message = message,
            NeedsAttention = needsAttention,
            FailoverType = failoverType,
            PrimaryDeviceId = primaryId,
            BackupDeviceId = backupId,
            LogId = logId,
        };
}
