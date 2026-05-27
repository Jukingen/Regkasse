using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Validators;

namespace KasseAPI_Final.Models.DTOs;

/// <summary>PATCH /api/admin/users/{id}/username — change login username (audited).</summary>
public class UpdateUsernameRequest
{
    [Required]
    [MinLength(3)]
    [MaxLength(50)]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Username may only contain letters, digits, underscores, and hyphens.")]
    [UsernameUnique]
    public string NewUsername { get; set; } = string.Empty;

    /// <summary>Optional operator note; recommended for audit compliance.</summary>
    [MaxLength(500)]
    public string? Reason { get; set; }
}
