using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Yönetici tarafından yapılandırılabilir ödeme yöntemi: sabit kod, görünen ad, RKSV için Invoice.PaymentMethod ile uyumlu 0–5 legacy eşlemesi.
/// </summary>
[Table("payment_method_definitions")]
public class PaymentMethodDefinition : ITenantEntity
{
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>FK to <see cref="Models.Tenant"/>; scopes codes and defaults per tenant.</summary>
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [Column("code")]
    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [Column("name")]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("is_default")]
    public bool IsDefault { get; set; }

    [Column("display_order")]
    public int DisplayOrder { get; set; }

    /// <summary>payment_details ve raporlarda saklanan Invoice.PaymentMethod sayısal değeri (0–5).</summary>
    [Column("legacy_payment_method_value")]
    public int LegacyPaymentMethodValue { get; set; }

    [Column("fiscal_category")]
    [MaxLength(64)]
    public string? FiscalCategory { get; set; }

    [Column("requires_terminal")]
    public bool RequiresTerminal { get; set; }

    [Column("terminal_type")]
    [MaxLength(64)]
    public string? TerminalType { get; set; }

    [Column("allow_refund")]
    public bool AllowRefund { get; set; } = true;

    [Column("icon")]
    [MaxLength(64)]
    public string? Icon { get; set; }

    /// <summary>Entegrasyon için isteğe bağlı JSON (terminal id, acquirer ipuçları).</summary>
    [Column("metadata_json")]
    public string? MetadataJson { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("updated_at_utc")]
    public DateTime? UpdatedAtUtc { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }
}
