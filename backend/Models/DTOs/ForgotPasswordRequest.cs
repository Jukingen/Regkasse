using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Models.DTOs;

/// <summary>POST /api/Auth/forgot-password — request a password reset email (admin app).</summary>
public sealed class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>Must be <c>admin</c> for this flow.</summary>
    public string? ClientApp { get; set; }
}
