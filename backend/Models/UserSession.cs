namespace KasseAPI_Final.Models;

/// <summary>
/// Active device/session exposed by <c>GET /api/user/sessions</c>.
/// Persisted as <see cref="AuthSession"/> in <c>auth_sessions</c> (plus hashed refresh tokens).
/// Never stores access or refresh token plaintext — sketch <c>Token</c> field intentionally omitted.
/// </summary>
public sealed class UserSession
{
    public Guid Id { get; set; }

    /// <summary>ASP.NET Identity user id (string), not a Guid.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Client surface: <c>admin</c>, <c>pos</c>, …</summary>
    public string ClientApp { get; set; } = string.Empty;

    /// <summary>Optional client-supplied device id (<c>X-Device-Id</c>).</summary>
    public string? DeviceId { get; set; }

    /// <summary>Friendly label derived from browser + OS (or client app).</summary>
    public string? DeviceName { get; set; }

    public string? Browser { get; set; }

    public string? OS { get; set; }

    public string? IPAddress { get; set; }

    /// <summary>Raw User-Agent (truncated); prefer <see cref="Browser"/> / <see cref="OS"/> for UI.</summary>
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastActiveAt { get; set; }

    /// <summary>Current refresh-token expiry for this session when known.</summary>
    public DateTime? ExpiresAt { get; set; }

    public bool IsActive { get; set; }

    public bool IsCurrent { get; set; }
}
