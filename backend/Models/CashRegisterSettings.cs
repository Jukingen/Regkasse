using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Per-tenant POS cash-register feature flags (ensure-ready, auto-open policy).
/// </summary>
[Table("cash_register_settings")]
public class CashRegisterSettings : ITenantEntity
{
    [Key]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    public virtual Tenant? Tenant { get; set; }

    [Column("effective_default_on_pos_entry")]
    public bool EffectiveDefaultOnPosEntry { get; set; } = true;

    [Column("auto_open_sole_closed_register")]
    public bool AutoOpenSoleClosedRegister { get; set; }

    [Column("auto_open_assigned_closed_register")]
    public bool AutoOpenAssignedClosedRegister { get; set; }

    [Column("default_auto_open_opening_balance", TypeName = "decimal(18,2)")]
    public decimal DefaultAutoOpenOpeningBalance { get; set; }

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; }
}
