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
        public decimal SubTotal { get; set; } // Net Total before tax
        public decimal TaxAmount { get; set; } // Total VAT
        public decimal GrandTotal { get; set; } // Gross Total (Payable)

        // --- Breakdown ---
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
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; } // Gross price per unit
        public decimal TotalPrice { get; set; } // Gross total (qty * unitPrice)
        public decimal TaxRate { get; set; } // 10, 20, etc.
    }

    public class ReceiptTaxLineDTO
    {
        public decimal Rate { get; set; } // 20.0
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
