using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public sealed class CreateAdminFeedbackRequestDto
{
    [Required]
    [MaxLength(32)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional 1–5 score for ease-of-use / performance.</summary>
    [Range(1, 5)]
    public int? Rating { get; set; }

    [MaxLength(500)]
    public string? PagePath { get; set; }
}

public sealed class UpdateAdminFeedbackStatusRequestDto
{
    [Required]
    [MaxLength(32)]
    public string Status { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? ReviewerNote { get; set; }
}

public sealed class AdminFeedbackDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? Rating { get; set; }
    public string? PagePath { get; set; }
    public string SubmittedByUserId { get; set; } = string.Empty;
    public string? SubmittedByDisplayName { get; set; }
    /// <summary>Login username from Identity (resolved at read time for inbox).</summary>
    public string? SubmittedByUsername { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewerNote { get; set; }
}

public sealed class AdminFeedbackListResponseDto
{
    public IReadOnlyList<AdminFeedbackDto> Items { get; set; } = Array.Empty<AdminFeedbackDto>();
    public int Total { get; set; }
}
