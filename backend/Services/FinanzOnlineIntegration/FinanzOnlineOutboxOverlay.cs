namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Super Admin overlay for <c>FinanzOnlineOutbox</c>.
/// New FA saves store a complete snapshot. Incomplete rows are legacy Enabled-only values.
/// </summary>
public sealed class FinanzOnlineOutboxOverlay
{
    public bool? Enabled { get; set; }
    public int? PollIntervalSeconds { get; set; }
    public int? MaxAttempts { get; set; }
    public int? BaseDelaySeconds { get; set; }
    public int? BackoffCapSeconds { get; set; }
    public int? JitterMaxSeconds { get; set; }
    public int? ProcessingTimeoutSeconds { get; set; }

    public bool HasAny =>
        Enabled is not null
        || PollIntervalSeconds is not null
        || MaxAttempts is not null
        || BaseDelaySeconds is not null
        || BackoffCapSeconds is not null
        || JitterMaxSeconds is not null
        || ProcessingTimeoutSeconds is not null;

    public bool IsComplete =>
        Enabled is not null
        && PollIntervalSeconds is not null
        && MaxAttempts is not null
        && BaseDelaySeconds is not null
        && BackoffCapSeconds is not null
        && JitterMaxSeconds is not null
        && ProcessingTimeoutSeconds is not null;
}

public sealed class FinanzOnlineOutboxWorkerRange
{
    public int Min { get; init; }
    public int Max { get; init; }
    public int[] Values { get; init; } = [];
}

public static class FinanzOnlineOutboxWorkerLimits
{
    public static readonly FinanzOnlineOutboxWorkerRange PollIntervalSeconds = new()
    {
        Min = 1,
        Max = 300,
        Values = [1, 2, 5, 10, 15, 30, 60, 120, 300],
    };

    public static readonly FinanzOnlineOutboxWorkerRange MaxAttempts = new()
    {
        Min = 1,
        Max = 5,
        Values = [1, 2, 3, 4, 5],
    };

    public static readonly FinanzOnlineOutboxWorkerRange BaseDelaySeconds = new()
    {
        Min = 1,
        Max = 3600,
        Values = [10, 15, 30, 60, 120, 300, 600],
    };

    public static readonly FinanzOnlineOutboxWorkerRange BackoffCapSeconds = new()
    {
        Min = 60,
        Max = 86400,
        Values = [300, 600, 1800, 3600, 7200, 86400],
    };

    public static readonly FinanzOnlineOutboxWorkerRange JitterMaxSeconds = new()
    {
        Min = 0,
        Max = 300,
        Values = [0, 5, 10, 15, 30, 60],
    };

    public static readonly FinanzOnlineOutboxWorkerRange ProcessingTimeoutSeconds = new()
    {
        Min = 30,
        Max = 3600,
        Values = [60, 120, 300, 600, 900, 1800],
    };

    public static void EnsureInRange(string field, int value, FinanzOnlineOutboxWorkerRange range)
    {
        if (value < range.Min || value > range.Max)
            throw new FinanzOnlineOutboxWorkerValidationException(field, range.Min, range.Max);
    }
}

public sealed class FinanzOnlineOutboxWorkerValidationException : InvalidOperationException
{
    public const string ErrorCode = "FO_OUTBOX_WORKER_INVALID";

    public FinanzOnlineOutboxWorkerValidationException(string field, int min, int max)
        : base($"Value for {field} must be between {min} and {max}.")
    {
        Field = field;
        Min = min;
        Max = max;
    }

    public string Field { get; }
    public int Min { get; }
    public int Max { get; }
}
