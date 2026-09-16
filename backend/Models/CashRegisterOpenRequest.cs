using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// POS cashier request for Mandanten-Admin (Manager) to open a closed cash register.
/// Approve opens the register as the requesting cashier so they can select it and start a shift.
/// </summary>
[Table("cash_register_open_requests")]
public class CashRegisterOpenRequest : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    [Column("cash_register_id")]
    public Guid CashRegisterId { get; set; }

    [ForeignKey(nameof(CashRegisterId))]
    public virtual CashRegister? CashRegister { get; set; }

    /// <summary><see cref="CashRegisterOpenRequestStatuses"/>.</summary>
    [Required]
    [MaxLength(16)]
    [Column("status")]
    public string Status { get; set; } = CashRegisterOpenRequestStatuses.Pending;

    [Required]
    [MaxLength(450)]
    [Column("requested_by_user_id")]
    public string RequestedByUserId { get; set; } = string.Empty;

    [Column("requested_at")]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    [Column("note")]
    public string? Note { get; set; }

    [MaxLength(450)]
    [Column("resolved_by_user_id")]
    public string? ResolvedByUserId { get; set; }

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    [MaxLength(500)]
    [Column("resolution_note")]
    public string? ResolutionNote { get; set; }
}

public static class CashRegisterOpenRequestStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Denied = "Denied";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Pending,
        Approved,
        Denied,
        Cancelled,
    };

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) && All.Contains(status.Trim());
}
