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

public sealed class InviteTenantUserRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string Role { get; set; } = string.Empty;

    public bool IsOwner { get; set; }
}

public sealed record TenantUserInviteResultDto(
    TenantUserDto User,
    bool UserCreated,
    bool InvitationEmailSent,
    string? EmailDeliveryNote,
    string? GeneratedPassword);

public sealed class UpdateTenantUserRoleRequest
{
    [Required]
    [MaxLength(64)]
    public string Role { get; set; } = string.Empty;
}

public sealed class ResetTenantUserPasswordRequest
{
    /// <summary>When true and SMTP is configured, email the new password to the user.</summary>
    public bool SendEmail { get; set; } = true;
}

public sealed record TenantUserPasswordResetResultDto(
    string UserId,
    string Email,
    string GeneratedPassword,
    string DeliveryNote,
    bool EmailSent,
    bool SmtpConfigured,
    bool ForcePasswordChangeOnNextLogin);
