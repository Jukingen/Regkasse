namespace KasseAPI_Final.Models;

/// <summary>
/// Active login session exposed by the API. Persisted as <see cref="AuthSession"/> in <c>auth_sessions</c>.
/// Device labels (<see cref="Browser"/> / <see cref="OS"/>) are derived from <see cref="UserAgent"/> at read time.
/// See also <see cref="UserSession"/> for the product-facing naming of this concept.
/// </summary>
public sealed class ActiveSession
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ClientApp { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? Browser { get; set; }
    public string? OS { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime LastActivityAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsActive { get; set; }
    public bool IsCurrent { get; set; }
}
