using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

[Table("offline_orders")]
public class OfflineOrder : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
    public Guid CashRegisterId { get; set; }
    public virtual CashRegister CashRegister { get; set; } = null!;
    public string OfflineOrderId { get; set; } = null!; // OFFLINE-{timestamp}-{random}
    public string OrderData { get; set; } = null!; // JSONB
    public decimal OrderTotal { get; set; }
    public string PaymentMethod { get; set; } = null!;
    public string Status { get; set; } = null!; // pending, synced, failed, expired
    public Guid? SyncedPaymentId { get; set; }
    public virtual PaymentDetails? SyncedPayment { get; set; }
    public string? SyncedInvoiceNumber { get; set; }
    public int SyncAttempts { get; set; }
    public DateTime? LastSyncAttemptUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; } // 72 hours from creation
    public DateTime? SyncedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
}
