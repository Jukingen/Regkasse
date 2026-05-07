namespace KasseAPI_Final.Services;

public sealed class NtpSyncCycleResult
{
    public bool Ran { get; init; }

    public bool LogicalSuccess { get; init; }

    public double? AverageOffsetSeconds { get; init; }

    public string Message { get; init; } = string.Empty;
}
