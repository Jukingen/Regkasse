using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Services.AdminTenants;

public sealed record TenantUserDto(
    string UserId,
    string Email,
    string Name,
    string Role,
    bool IsOwner,
    DateTime JoinedAtUtc);

public sealed class AddAdminTenantUserRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string Role { get; set; } = string.Empty;

    public bool IsOwner { get; set; }
}

public sealed class UpdateAdminTenantUserRequest
{
    [MaxLength(64)]
    public string? Role { get; set; }

    public bool? IsOwner { get; set; }
}

public sealed class CreateTenantUserRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? FirstName { get; set; }

    [MaxLength(50)]
    public string? LastName { get; set; }

    [Required]
    [MaxLength(64)]
    public string Role { get; set; } = string.Empty;

    public bool IsOwner { get; set; }
}

/// <summary>One-click demo/test user: role only; email and password are generated server-side.</summary>
public sealed class CreateQuickTenantUserRequest
{
    [Required]
    [MaxLength(64)]
    public string Role { get; set; } = string.Empty;
}

public sealed record CreateTenantUserResultDto(
    string UserId,
    string Email,
    string GeneratedPassword,
    bool ForcePasswordChangeOnNextLogin,
    bool Success,
    string? TenantPortalUrl = null,
    string? Role = null);

public sealed class UpdateTenantUserRoleRequest
{
    [Required]
    [MaxLength(64)]
    public string Role { get; set; } = string.Empty;
}

public sealed record TenantUserPasswordResetResultDto(
    string UserId,
    string Email,
    string GeneratedPassword,
    string DeliveryNote,
    bool ForcePasswordChangeOnNextLogin);
