namespace KasseAPI_Final.Models.DTOs;

/// <summary>GET /api/user/profile — current user profile for self-service display.</summary>
public sealed class UserProfileDto
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? PhoneNumber { get; set; }
}
