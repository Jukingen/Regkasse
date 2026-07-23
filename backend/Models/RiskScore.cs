using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Canonical risk level labels persisted on <see cref="RiskScore"/>.</summary>
public static class RiskLevels
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Critical = "Critical";

    public static bool IsValid(string? level) =>
        level is Low or Medium or High or Critical;
}

/// <summary>
/// Persisted risk evaluation for a tenant user action (anomaly / scoring pipeline).
/// </summary>
[Table("risk_scores")]
public class RiskScore : BaseEntity, ITenantEntity
{
    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>AspNetUsers Id (string Identity key).</summary>
    [Required]
    [MaxLength(450)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    [Column("action_type")]
    public string ActionType { get; set; } = string.Empty;

    /// <summary>0–100 cumulative score from risk rules.</summary>
    [Column("score")]
    public int Score { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("risk_level")]
    public string RiskLevel { get; set; } = RiskLevels.Low;

    [Required]
    [Column("reason", TypeName = "text")]
    public string Reason { get; set; } = string.Empty;

    [Column("is_resolved")]
    public bool IsResolved { get; set; }

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    [MaxLength(450)]
    [Column("resolved_by")]
    public string? ResolvedBy { get; set; }

    [Column("resolution", TypeName = "text")]
    public string? Resolution { get; set; }

    [Column("details_json", TypeName = "jsonb")]
    public string? DetailsJson { get; set; }
}
