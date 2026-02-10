export interface ReceiptDTO {
    // --- Header ---
    receiptId: string; // Unique ID (UUID)
    receiptNumber: string; // Belegnummer (e.g. 12345)
    date: string; // ISO string
    cashierName: string;
    tableNumber?: number; // Optional

    company: {
        name: string;
        address: string;
        taxNumber: string; // UID-Nummer
    };

    kassenID: string; // Kassen-ID (RKSV)

    // --- Items ---
    items: ReceiptItemDTO[];

    // --- VAT Breakdown (RKSV requirement) ---
    taxRates: ReceiptTaxRateDTO[];

    // --- Totals ---
    subtotal: number; // Net total
    taxAmount: number; // Total VAT
    grandTotal: number; // Brutto total

    // --- Payments ---
    payments: ReceiptPaymentDTO[];

    footerText?: string;

    // --- Signature Block (RKSV) ---
    signature: {
        algorithm: string; // e.g., "ES256"
        value: string; // The JWS signature
        serialNumber: string; // Certificate serial
        timestamp: string; // Signature time
        qrData: string; // Complete payload for QR
    };
}

export interface ReceiptItemDTO {
    name: string;
    quantity: number;
    unitPrice: number; // Brutto price per unit
    totalPrice: number; // Brutto line total
    taxRate: number; // e.g., 20, 10, 0
}

export interface ReceiptTaxRateDTO {
    rate: number; // 20, 10, 0
    netAmount: number; // Net turnover for this rate
    taxAmount: number; // Tax amount for this rate
    grossAmount: number; // Gross turnover for this rate
}

export interface ReceiptPaymentDTO {
    method: 'cash' | 'card' | 'voucher' | 'transfer';
    amount: number;
    tendered?: number; // Only for cash
    change?: number; // Only for cash
}
