namespace KasseAPI_Final.Models;

/// <summary>
/// Active login session exposed by the API. Persisted as <see cref="AuthSession"/> in <c>auth_sessions</c>.
/// </summary>
public sealed class ActiveSession
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ClientApp { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public string? IpAddress { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime LastActivityAtUtc { get; set; }
    public bool IsActive { get; set; }
    public bool IsCurrent { get; set; }
}
