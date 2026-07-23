namespace KasseAPI_Final.Services.Tse;

/// <summary>Result of a per-device TSE health evaluation (persisted onto <see cref="Models.TseDevice"/>).</summary>
public sealed class TseHealthResult
{
    public Guid DeviceId { get; init; }

    public bool IsHealthy { get; init; }

    public int HealthScore { get; init; }

    public Models.TseHealthStatus Status { get; init; }

    public string Message { get; init; } = string.Empty;

    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Wall-clock duration of the health probe in milliseconds.</summary>
    public int? ResponseTimeMs { get; set; }

    public static TseHealthResult Fail(Guid deviceId, string message, Models.TseHealthStatus status = Models.TseHealthStatus.Offline) =>
        new()
        {
            DeviceId = deviceId,
            IsHealthy = false,
            HealthScore = 0,
            Status = status,
            Message = message,
            CheckedAt = DateTime.UtcNow,
        };

    public static TseHealthResult Fail(string message) =>
        Fail(Guid.Empty, message);
}
