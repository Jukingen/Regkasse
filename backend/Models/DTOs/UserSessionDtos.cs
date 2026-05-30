namespace KasseAPI_Final.Models.DTOs;

public sealed class ActiveSessionDto
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

public sealed class TenantSessionPolicyDto
{
    public int SessionTimeoutMinutes { get; set; } = 30;
    public int WarningBeforeTimeoutMinutes { get; set; } = 1;
    public bool KeepCartAfterTimeout { get; set; } = true;
    public bool IdleTimeoutEnabled { get; set; } = true;
}
