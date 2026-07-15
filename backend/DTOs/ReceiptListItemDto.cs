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

        /// <summary>RKSV Sonderbeleg marker for list UI (e.g. Nullbeleg).</summary>
        public string? RksvSpecialReceiptKind { get; set; }

        /// <summary>Vienna calendar year when <see cref="RksvSpecialReceiptKind"/> is set (Monatsbeleg guard).</summary>
        public int? RksvSpecialReceiptYear { get; set; }

        /// <summary>Vienna calendar month 1–12 when applicable.</summary>
        public int? RksvSpecialReceiptMonth { get; set; }

        /// <summary>FinanzOnline/BMF submission lifecycle for Startbeleg/Jahresbeleg; null when no tracking row.</summary>
        public string? RksvFinanzOnlineSubmissionStatus { get; set; }

        /// <summary>True when this Sonderbeleg was created nachträglich / past its legal deadline.</summary>
        public bool IsLateCreated { get; set; }

        /// <summary>Operator reason when <see cref="IsLateCreated"/> is true.</summary>
        public string? LateCreationReason { get; set; }

        /// <summary>Canonical end date of the RKSV period covered by this receipt.</summary>
        public DateTime? IntendedPeriodDate { get; set; }

        /// <summary>True when a persisted RKSV receipt PDF exists for download.</summary>
        public bool HasStoredPdf { get; set; }
    }
}
