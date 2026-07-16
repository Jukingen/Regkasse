using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// POS cashier shift snapshot (operational, not RKSV fiscal). Tied to a cash register session.
/// Register open/close is still authoritative via <see cref="CashRegisterShiftService"/>.
/// </summary>
[Table("cashier_shifts")]
public class CashierShift : BaseEntity, ITenantEntity
{
    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    [Required]
    [Column("cash_register_id")]
    public Guid CashRegisterId { get; set; }

    [ForeignKey(nameof(CashRegisterId))]
    public virtual CashRegister? CashRegister { get; set; }

    [Required]
    [MaxLength(450)]
    [Column("cashier_id")]
    public string CashierId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("cashier_name")]
    public string CashierName { get; set; } = string.Empty;

    [Required]
    [Column("start_balance", TypeName = "decimal(18,2)")]
    public decimal StartBalance { get; set; }

    [Column("end_balance", TypeName = "decimal(18,2)")]
    public decimal EndBalance { get; set; }

    [Column("total_sales", TypeName = "decimal(18,2)")]
    public decimal TotalSales { get; set; }

    [Column("total_cash", TypeName = "decimal(18,2)")]
    public decimal TotalCash { get; set; }

    [Column("total_card", TypeName = "decimal(18,2)")]
    public decimal TotalCard { get; set; }

    [Column("difference", TypeName = "decimal(18,2)")]
    public decimal Difference { get; set; }

    [Required]
    [Column("started_at", TypeName = "timestamptz")]
    public DateTime StartedAt { get; set; }

    [Column("ended_at", TypeName = "timestamptz")]
    public DateTime? EndedAt { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = CashierShiftStatuses.Active;

    [Column("notes", TypeName = "text")]
    public string? Notes { get; set; }

    /// <summary>FK to fiscal <see cref="DailyClosing"/> row after POS Tagesabschluss.</summary>
    [Column("daily_closing_id")]
    public Guid? DailyClosingId { get; set; }

    [ForeignKey(nameof(DailyClosingId))]
    public virtual DailyClosing? DailyClosing { get; set; }

    /// <summary>Physically counted cash at daily closing (operational).</summary>
    [Column("cash_count", TypeName = "decimal(18,2)")]
    public decimal? CashCount { get; set; }

    /// <summary>True when opened by auto-open (login / ensure-ready), not manual StartShift.</summary>
    [Column("is_auto_opened")]
    public bool IsAutoOpened { get; set; }

    /// <summary>True when closed by auto-close (logout / soft complete), not manual EndShift.</summary>
    [Column("is_auto_closed")]
    public bool IsAutoClosed { get; set; }
}

public static class CashierShiftStatuses
{
    public const string Active = "Active";
    public const string Completed = "Completed";
    public const string Discrepancy = "Discrepancy";
}
