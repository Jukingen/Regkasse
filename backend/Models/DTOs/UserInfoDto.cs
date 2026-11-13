namespace KasseAPI_Final.Models.DTOs;

/// <summary>Compact actor/user snapshot for API responses (audit logs, etc.).</summary>
public sealed class UserInfoDto
{
    /// <summary>Identity user id (string GUID).</summary>
    public string Id { get; set; } = string.Empty;

    public string? UserName { get; set; }

    public string? Email { get; set; }

    /// <summary>Preferred display label (FirstName LastName, else UserName).</summary>
    public string? DisplayName { get; set; }

    public string? Role { get; set; }
}
