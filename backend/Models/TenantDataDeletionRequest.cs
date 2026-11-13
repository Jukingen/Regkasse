using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Mandant request to export and/or purge non-RKSV customer data after license expiry (Archived).
/// Confirmation starts a 7-day wait; then auto-purge or Super Admin execute.
/// Fiscal/RKSV rows remain for legal retention (7 years).
/// </summary>
[Table("tenant_data_deletion_requests")]
public class TenantDataDeletionRequest : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    /// <summary><see cref="TenantDataDeletionRequestStatuses"/>.</summary>
    [Required]
    [MaxLength(24)]
    [Column("status")]
    public string Status { get; set; } = TenantDataDeletionRequestStatuses.Pending;

    [MaxLength(450)]
    [Column("requested_by_user_id")]
    public string? RequestedByUserId { get; set; }

    [Column("requested_at_utc")]
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    [Column("reason")]
    public string? Reason { get; set; }

    [Column("export_completed_at_utc")]
    public DateTime? ExportCompletedAtUtc { get; set; }

    /// <summary>FA confirmation by Mandanten-Admin (or Super Admin). Starts the 7-day wait.</summary>
    [Column("confirmed_at_utc")]
    public DateTime? ConfirmedAtUtc { get; set; }

    [MaxLength(450)]
    [Column("confirmed_by_user_id")]
    public string? ConfirmedByUserId { get; set; }

    /// <summary>When the irreversible non-RKSV purge finished.</summary>
    [Column("completed_at_utc")]
    public DateTime? CompletedAtUtc { get; set; }

    [MaxLength(450)]
    [Column("completed_by_user_id")]
    public string? CompletedByUserId { get; set; }

    /// <summary><c>auto</c> (hosted service) or <c>manual</c> (Super Admin execute).</summary>
    [MaxLength(16)]
    [Column("executed_via")]
    public string? ExecutedVia { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

public static class TenantDataDeletionRequestStatuses
{
    public const string Pending = "pending";
    public const string Confirmed = "confirmed";
    public const string ExportReady = "export_ready";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
}

public static class TenantDataDeletionExecutedVia
{
    public const string Auto = "auto";
    public const string Manual = "manual";
}
