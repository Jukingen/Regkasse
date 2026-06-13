using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Models.DTOs;

/// <summary>PUT /api/user/profile — self-service profile fields (no role/username changes).</summary>
public sealed class UpdateProfileRequest
{
    [Required]
    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }
}
