using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KasseAPI_Final.DTOs;

public sealed class DeploymentCiReportRequest
{
    [Required]
    [MaxLength(32)]
    public string Stage { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string Status { get; set; } = string.Empty;

    public string? GitSha { get; set; }

    public string? GitRef { get; set; }

    public string? ImageTag { get; set; }

    public string? PreviousImageTag { get; set; }

    public IReadOnlyList<string>? TenantIds { get; set; }

    public string? ErrorMessage { get; set; }

    public string? RunUrl { get; set; }

    public string? TriggeredBy { get; set; }

    public bool? SmokePassed { get; set; }

    public string? SmokeSummary { get; set; }

    /// <summary>Optional canary soak hours when recording per-tenant history (default from config).</summary>
    public int? SoakHours { get; set; }
}

public sealed class DeploymentRunDto
{
    public Guid Id { get; set; }

    public string Stage { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? GitSha { get; set; }

    public string? GitRef { get; set; }

    public string? ImageTag { get; set; }

    public string? PreviousImageTag { get; set; }

    public IReadOnlyList<string> TenantIds { get; set; } = Array.Empty<string>();

    public string? ErrorMessage { get; set; }

    public string? RunUrl { get; set; }

    public string? TriggeredBy { get; set; }

    public bool? SmokePassed { get; set; }

    public string? SmokeSummary { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class DeploymentRunListResponseDto
{
    public IReadOnlyList<DeploymentRunDto> Items { get; set; } = Array.Empty<DeploymentRunDto>();

    public int Total { get; set; }

    [JsonPropertyName("latestByStage")]
    public IReadOnlyDictionary<string, DeploymentRunDto?> LatestByStage { get; set; }
        = new Dictionary<string, DeploymentRunDto?>();
}

public sealed class DeploymentRollbackRequest
{
    [Required]
    [MaxLength(32)]
    public string Stage { get; set; } = string.Empty;

    /// <summary>Must be exactly <c>rollback</c>.</summary>
    [Required]
    [MaxLength(32)]
    public string Confirm { get; set; } = string.Empty;

    /// <summary>Optional override; otherwise uses latest run previousImageTag.</summary>
    public string? PreviousImageTag { get; set; }
}

public sealed class DeploymentRollbackResultDto
{
    public bool Invoked { get; set; }

    public string Stage { get; set; } = string.Empty;

    public string? PreviousImageTag { get; set; }

    public string Message { get; set; } = string.Empty;
}
