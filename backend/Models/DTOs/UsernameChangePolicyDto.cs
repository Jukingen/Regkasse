namespace KasseAPI_Final.Models.DTOs;

/// <summary>GET /api/UserManagement/me/username-change-policy — self-service username cooldown.</summary>
public sealed class UsernameChangePolicyDto
{
    public int CooldownDays { get; set; }
    public bool CanChange { get; set; }
    /// <summary>False when the current actor (e.g. SuperAdmin) is exempt from cooldown and new-account rules.</summary>
    public bool RestrictionsApply { get; set; } = true;
    public DateTime? LastChangedAtUtc { get; set; }
    public DateTime? NextChangeAllowedAtUtc { get; set; }
}
