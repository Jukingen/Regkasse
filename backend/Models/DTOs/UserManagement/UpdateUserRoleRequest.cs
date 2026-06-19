using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Models.DTOs.UserManagement;

/// <summary>
/// Role assignment payload for user role update endpoints (tenant-scoped and user management).
/// </summary>
public class UpdateUserRoleRequest
{
    [Required]
    [MaxLength(64)]
    public string Role { get; set; } = string.Empty;
}
