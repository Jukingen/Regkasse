using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Post-create guided onboarding checklist for a mandant (non-fiscal).</summary>
[Table("tenant_onboarding_status")]
public class TenantOnboardingStatus
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    /// <summary>AccountCreated | ProductsImported | StartbelegDone | TrainingComplete</summary>
    [Required]
    [MaxLength(40)]
    [Column("step")]
    public string Step { get; set; } = TenantOnboardingSteps.AccountCreated;

    [Column("is_completed")]
    public bool IsCompleted { get; set; }

    [Column("completed_at_utc")]
    public DateTime? CompletedAtUtc { get; set; }

    [MaxLength(450)]
    [Column("completed_by_user_id")]
    public string? CompletedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

public static class TenantOnboardingSteps
{
    public const string AccountCreated = "AccountCreated";
    public const string ProductsImported = "ProductsImported";
    public const string StartbelegDone = "StartbelegDone";
    public const string TrainingComplete = "TrainingComplete";

    public static readonly string[] DefaultOrder =
    [
        AccountCreated,
        ProductsImported,
        StartbelegDone,
        TrainingComplete,
    ];
}
