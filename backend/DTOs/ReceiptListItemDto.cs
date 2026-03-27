using System;

namespace KasseAPI_Final.DTOs
{
    /// <summary>
    /// List item DTO for admin receipts list (paginated).
    /// </summary>
    public class ReceiptListItemDto
    {
        public Guid ReceiptId { get; set; }
        /// <summary>Zugehörige Zahlung — für Nachdruck-Flow (by-payment) ohne Extra-Lookup.</summary>
        public Guid PaymentId { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public string? CashierId { get; set; }
        /// <summary>Authoritative register FK (same as receipt / payment <c>CashRegisterId</c>).</summary>
        public Guid CashRegisterEntityId { get; set; }
        /// <summary>Legacy list field: <see cref="CashRegisterEntityId"/> as string (UUID). Prefer <see cref="CashRegisterEntityId"/> for typing.</summary>
        public string CashRegisterId { get; set; } = string.Empty;
        /// <summary>Human-facing register number from <c>CashRegisters.RegisterNumber</c> (not the FK).</summary>
        public string RegisterDisplayNumber { get; set; } = string.Empty;
        public decimal SubTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
