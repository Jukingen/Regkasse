using System;
using System.Collections.Generic;

namespace KasseAPI_Final.DTOs
{
    /// <summary>
    /// RKSV-compliant receipt data transfer object
    /// </summary>
    public class ReceiptDTO
    {
        // --- Header ---
        public Guid ReceiptId { get; set; } // UUID
        /// <summary>Source payment row for this fiscal receipt (navigation / forensic).</summary>
        public Guid PaymentId { get; set; }
        /// <summary>Authoritative cash register identity: FK to <c>cash_registers.Id</c> (same value stored on receipt row).</summary>
        public Guid CashRegisterId { get; set; }
        /// <summary>When this receipt is a reversal: original sale payment id.</summary>
        public Guid? OriginalPaymentId { get; set; }
        /// <summary>When this receipt is a reversal: original sale receipt id.</summary>
        public Guid? OriginalSaleReceiptId { get; set; }
        /// <summary>null = normal sale; Storno; Refund.</summary>
        public string? FiscalTraceKind { get; set; }
        /// <summary>True when payment is linked to a controlled offline intent replay.</summary>
        public bool HasOfflineOrigin { get; set; }
        public Guid? OfflineTransactionId { get; set; }
        /// <summary>Device-time UTC when offline queue entry was created (if HasOfflineOrigin).</summary>
        public DateTime? OfflineCreatedAtUtc { get; set; }
        /// <summary>Server UTC when offline replay produced the fiscal payment (if HasOfflineOrigin).</summary>
        public DateTime? FiscalizedAtUtc { get; set; }

        /// <summary>Clock drift warning flag raised during offline intent creation (if provided by client).</summary>
        public bool ClockDriftWarning { get; set; }

        /// <summary>Client sequence gap detected during offline intent creation (missing sequence numbers).</summary>
        public bool SequenceGapDetected { get; set; }

        /// <summary>Client sequence duplicate/non-monotonic detected during offline intent creation.</summary>
        public bool SequenceDuplicateDetected { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty; // Belegnummer (e.g. 12345)
        /// <summary>Receipt issued-at (Belegzeit, fiscal).</summary>
        public DateTime Date { get; set; }
        /// <summary>When the receipt row was persisted (may differ slightly from issued-at).</summary>
        public DateTime ReceiptPersistedAtUtc { get; set; }
        public string CashierId { get; set; } = string.Empty;
        public string? CashierDisplayName { get; set; }
        public int? TableNumber { get; set; } // Optional
        /// <summary>RKSV / receipt display register id: <c>CashRegisters.RegisterNumber</c>, not the register row GUID. Same text as <see cref="DisplayRegisterNumber"/>.</summary>
        public string KassenID { get; set; } = string.Empty;
        /// <summary>Explicit alias for <see cref="KassenID"/> (display register number). Kept alongside legacy <c>kassenID</c> JSON for clarity.</summary>
        public string DisplayRegisterNumber { get; set; } = string.Empty;

        public ReceiptCompanyDTO Company { get; set; } = new();
        public ReceiptHeaderDTO Header { get; set; } = new(); // Optional extra header info

        // --- Items ---
        public List<ReceiptItemDTO> Items { get; set; } = new();

        // --- Totals ---
        public decimal SubTotal { get; set; } // Net Total before tax (= TotalNet)
        public decimal TaxAmount { get; set; } // Total VAT (= TotalVat)
        public decimal GrandTotal { get; set; } // Gross Total (Payable = TotalGross)
        /// <summary>Fiş toplamları (net, vergi, brüt); SubTotal/TaxAmount/GrandTotal ile aynı kaynak.</summary>
        public ReceiptTotalsDTO? Totals { get; set; }

        // --- VAT Breakdown ---
        public List<ReceiptTaxLineDTO> TaxRates { get; set; } = new();

        // --- Payments ---
        public List<ReceiptPaymentDTO> Payments { get; set; } = new();

        // --- Footer / RKSV ---
        public ReceiptSignatureDTO? Signature { get; set; }
        public string FooterText { get; set; } = "Vielen Dank für Ihren Besuch!";
    }

    public class ReceiptCompanyDTO
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string TaxNumber { get; set; } = string.Empty; // UID / Steuernummer
    }

    public class ReceiptHeaderDTO
    {
        public string ShopName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    public class ReceiptItemDTO
    {
        public Guid? ItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        /// <summary>Birim fiyat (brüt, inkl. MwSt).</summary>
        public decimal UnitPrice { get; set; }
        /// <summary>Satır toplam brüt (lineTotalGross).</summary>
        public decimal TotalPrice { get; set; }
        /// <summary>Satır net tutarı.</summary>
        public decimal LineTotalNet { get; set; }
        /// <summary>Satır brüt tutarı (TotalPrice ile aynı).</summary>
        public decimal LineTotalGross { get; set; }
        /// <summary>Vergi oranı yüzde (10, 20).</summary>
        public decimal TaxRate { get; set; }
        /// <summary>Vergi oranı kesir (0.10, 0.20).</summary>
        public decimal VatRate { get; set; }
        /// <summary>Satır vergi tutarı.</summary>
        public decimal VatAmount { get; set; }
        /// <summary>Kategori adı (opsiyonel).</summary>
        public string? CategoryName { get; set; }
        /// <summary>Phase 2 legacy: parent line ID when this is a nested modifier line. New receipts are flat (null).</summary>
        public Guid? ParentItemId { get; set; }
        /// <summary>Phase 2 legacy: true when line is nested under a product (e.g. "+ Extra Cheese"). New receipts: all false (product-only lines).</summary>
        public bool IsModifierLine { get; set; }
    }

    /// <summary>Fiş toplamları: net, vergi, brüt.</summary>
    public class ReceiptTotalsDTO
    {
        public decimal TotalNet { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalGross { get; set; }
    }

    /// <summary>VAT dökümü satırı (vergi oranına göre gruplanmış).</summary>
    public class ReceiptTaxLineDTO
    {
        public int TaxType { get; set; }
        /// <summary>Vergi oranı yüzde (10, 20).</summary>
        public decimal Rate { get; set; }
        /// <summary>Vergi oranı kesir (0.10, 0.20).</summary>
        public decimal VatRate { get; set; }
        public decimal NetAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrossAmount { get; set; }
    }

    public class ReceiptPaymentDTO
    {
        public string Method { get; set; } = "cash"; // cash, card, etc.
        public decimal Amount { get; set; }
        public decimal Tendered { get; set; } // For cash: amount given by customer
        public decimal Change { get; set; } // For cash: change returned
    }

    public class ReceiptSignatureDTO
    {
        public string Algorithm { get; set; } = "ES256";
        public string SerialNumber { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string PrevSignatureValue { get; set; } = string.Empty;
        public string SignatureValue { get; set; } = string.Empty; // Compact JWS
        public string QrData { get; set; } = string.Empty;
    }
}
