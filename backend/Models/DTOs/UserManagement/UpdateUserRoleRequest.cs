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

    /// <summary>
    /// When true and the role changes, permissions from the previous role that are not in the new role
    /// defaults are persisted as user-level grant overrides.
    /// </summary>
    public bool PreservePreviousPermissions { get; set; }
}
