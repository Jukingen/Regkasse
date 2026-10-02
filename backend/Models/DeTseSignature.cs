using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// SIGN DE signature for one payment. Not an RKSV JWS and not read by DEP export.
/// </summary>
[Table("de_tse_signatures")]
public class DeTseSignature : BaseEntity, ITenantEntity
{
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("payment_details_id")]
    public Guid PaymentDetailsId { get; set; }

    [ForeignKey(nameof(PaymentDetailsId))]
    public PaymentDetails? Payment { get; set; }

    [Column("tss_id")]
    [MaxLength(64)]
    public string TssId { get; set; } = string.Empty;

    [Column("transaction_id")]
    [MaxLength(64)]
    public string TransactionId { get; set; } = string.Empty;

    [Column("signature")]
    public string Signature { get; set; } = string.Empty;

    [Column("signature_algorithm")]
    [MaxLength(32)]
    public string? SignatureAlgorithm { get; set; }

    [Column("signed_at_utc")]
    public DateTime SignedAtUtc { get; set; }

    [Column("certificate_serial")]
    [MaxLength(128)]
    public string? CertificateSerial { get; set; }
}
