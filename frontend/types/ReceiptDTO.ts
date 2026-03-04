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
    subtotal: number; // Net total (= totalNet)
    taxAmount: number; // Total VAT
    grandTotal: number; // Brutto total
    /** Backend'den gelen fiş toplamları (net/vat/gross) */
    totals?: {
        totalNet: number;
        totalVat: number;
        totalGross: number;
    };

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

    /** RKSV doğrulama URL'si (varsa fişte metin olarak basılır) */
    verificationUrl?: string;
}

export interface ReceiptItemDTO {
    itemId?: string;
    name: string;
    quantity: number;
    unitPrice: number; // Brutto price per unit
    totalPrice: number; // Brutto line total (= lineTotalGross)
    /** Satır net tutarı (backend'den) */
    lineTotalNet?: number;
    /** Satır brüt tutarı (totalPrice ile aynı) */
    lineTotalGross?: number;
    taxRate: number; // yüzde: 20, 10, 0
    /** Vergi oranı kesir (0.20, 0.10) – backend'den */
    vatRate?: number;
    /** Satır vergi tutarı */
    vatAmount?: number;
    categoryName?: string | null;
    parentItemId?: string | null;
    isModifierLine?: boolean;
}

export interface ReceiptTaxRateDTO {
    taxType?: number;
    rate: number; // yüzde: 20, 10, 0
    /** Vergi oranı kesir (0.20, 0.10) – backend'den */
    vatRate?: number;
    netAmount: number;
    taxAmount: number;
    grossAmount: number;
}

export interface ReceiptPaymentDTO {
    method: 'cash' | 'card' | 'voucher' | 'transfer';
    amount: number;
    tendered?: number; // Only for cash
    change?: number; // Only for cash
}
