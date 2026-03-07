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
        public string ReceiptNumber { get; set; } = string.Empty; // Belegnummer (e.g. 12345)
        public DateTime Date { get; set; } // Issued At
        public string CashierName { get; set; } = string.Empty;
        public int? TableNumber { get; set; } // Optional
        public string KassenID { get; set; } = string.Empty; // Cash Register ID

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
