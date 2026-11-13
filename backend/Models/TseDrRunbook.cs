using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Super Admin TSE disaster-recovery runbook (operational drill/plan; not fiscal evidence).
/// </summary>
[Table("tse_dr_runbooks")]
public class TseDrRunbook
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary><see cref="TseDrScenarios"/>.</summary>
    [Required]
    [MaxLength(32)]
    [Column("scenario")]
    public string Scenario { get; set; } = TseDrScenarios.TseFailure;

    /// <summary><see cref="TseDrRunbookStatuses"/>.</summary>
    [Required]
    [MaxLength(24)]
    [Column("status")]
    public string Status { get; set; } = TseDrRunbookStatuses.Ready;

    [Column("estimated_rto_minutes")]
    public int EstimatedRtoMinutes { get; set; }

    [Column("actual_rto_minutes")]
    public int ActualRtoMinutes { get; set; }

    [Column("is_drill")]
    public bool IsDrill { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("last_tested_at")]
    public DateTime? LastTestedAt { get; set; }

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [MaxLength(450)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [MaxLength(2000)]
    [Column("summary")]
    public string? Summary { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    public virtual ICollection<TseDrStep> Steps { get; set; } = new List<TseDrStep>();
}

[Table("tse_dr_steps")]
public class TseDrStep
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("runbook_id")]
    public Guid RunbookId { get; set; }

    [Column("step_order")]
    public int Order { get; set; }

    [Required]
    [MaxLength(128)]
    [Column("action")]
    public string Action { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("is_automated")]
    public bool IsAutomated { get; set; }

    [Column("is_completed")]
    public bool IsCompleted { get; set; }

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [MaxLength(2000)]
    [Column("result")]
    public string? Result { get; set; }

    [MaxLength(2000)]
    [Column("error")]
    public string? Error { get; set; }

    [ForeignKey(nameof(RunbookId))]
    public virtual TseDrRunbook? Runbook { get; set; }
}

public static class TseDrScenarios
{
    public const string TseFailure = "TSEFailure";
    public const string NetworkIsolation = "NetworkIsolation";
    public const string DataCorruption = "DataCorruption";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        TseFailure, NetworkIsolation, DataCorruption,
    };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim());
}

public static class TseDrRunbookStatuses
{
    public const string Ready = "Ready";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Failed = "Failed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Ready, InProgress, Completed, Failed,
    };
}
