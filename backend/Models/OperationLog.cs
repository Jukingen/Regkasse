using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Mutable operation journal for reversible admin mutations (before/after JSON).
/// Separate from append-only <see cref="AuditLog"/> — fiscal payment/receipt ops must not be undone via this table.
/// </summary>
[Table("operation_logs")]
public class OperationLog : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>Identity user id (string).</summary>
    [Required]
    [MaxLength(450)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    [Column("operation_type")]
    public string OperationType { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    [Column("entity_type")]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    [Column("entity_id")]
    public string EntityId { get; set; } = string.Empty;

    [Column("before_state", TypeName = "jsonb")]
    public string? BeforeState { get; set; }

    [Column("after_state", TypeName = "jsonb")]
    public string? AfterState { get; set; }

    [Column("is_undone")]
    public bool IsUndone { get; set; }

    [MaxLength(450)]
    [Column("undone_by")]
    public string? UndoneBy { get; set; }

    [Column("undone_at")]
    public DateTime? UndoneAt { get; set; }

    [MaxLength(500)]
    [Column("reason")]
    public string? Reason { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(64)]
    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [MaxLength(512)]
    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }
}

/// <summary>Canonical operation type strings stored in <see cref="OperationLog.OperationType"/>.</summary>
public static class OperationTypes
{
    public const string UpdateProduct = "UpdateProduct";
    public const string UpdateCustomer = "UpdateCustomer";
    public const string CreateCategory = "CreateCategory";
    public const string CreateVoucher = "CreateVoucher";

    /// <summary>Logged for audit trail only — never undoable (RKSV).</summary>
    public const string CreatePayment = "CreatePayment";
}

/// <summary>Entity type labels for operation logs.</summary>
public static class OperationEntityTypes
{
    public const string Product = "Product";
    public const string Customer = "Customer";
    public const string Category = "Category";
    public const string Voucher = "Voucher";
    public const string Payment = "Payment";
}
