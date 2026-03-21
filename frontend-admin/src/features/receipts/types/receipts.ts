/**
 * Receipt DTOs — manual types matching the API contract.
 * List: GET /api/Receipts/list. Detail: GET /api/Receipts/{id} (mapped from ReceiptDTO in hook).
 */

// ---------------------------------------------------------------------------
// Query Params
// ---------------------------------------------------------------------------

/** Query params for GET /api/Receipts/list */
export interface ReceiptListParams {
    page: number;
    pageSize: number;
    sort?: string;                // e.g. "issuedAt:desc"
    receiptNumber?: string;
    cashRegisterId?: string;
    cashierId?: string;
    issuedFrom?: string;          // ISO 8601
    issuedTo?: string;            // ISO 8601
}

/** Defaults applied when searchParams are missing */
export const RECEIPT_LIST_DEFAULTS: ReceiptListParams = {
    page: 1,
    pageSize: 25,
    sort: 'issuedAt:desc',
};

// ---------------------------------------------------------------------------
// List Response
// ---------------------------------------------------------------------------

/** Single receipt in the paginated list */
export interface ReceiptListItemDto {
    receiptId: string;
    receiptNumber: string;
    issuedAt: string;
    cashierId: string | null;
    cashRegisterId: string;
    subTotal: number;
    taxTotal: number;
    grandTotal: number;
    createdAt: string;
}

/** Paginated list envelope */
export interface ReceiptListResponse {
    items: ReceiptListItemDto[];
    page: number;
    pageSize: number;
    totalCount: number;
}

// ---------------------------------------------------------------------------
// Detail Response
// ---------------------------------------------------------------------------

/** Line item within a receipt */
export interface ReceiptItemDto {
    itemId: string;
    productName: string;
    quantity: number;
    unitPrice: number;
    totalPrice: number;
    taxRate: number;
}

/** Tax breakdown line */
export interface ReceiptTaxLineDto {
    lineId: string;
    taxRate: number;
    netAmount: number;
    taxAmount: number;
    grossAmount: number;
}

/** Full receipt detail (includes items + taxLines) */
export interface ReceiptDetailDto {
    receiptId: string;
    paymentId: string | null;
    receiptNumber: string;
    issuedAt: string;
    cashierId: string | null;
    cashierDisplayName?: string | null;
    cashRegisterId: string;
    subTotal: number;
    taxTotal: number;
    grandTotal: number;
    qrCodePayload: string | null;
    signatureValue: string | null;
    prevSignatureValue: string | null;
    createdAt: string;
    items: ReceiptItemDto[];
    taxLines: ReceiptTaxLineDto[];
    /** Storno | Refund when reversal receipt */
    fiscalTraceKind?: string | null;
    originalPaymentId?: string | null;
    originalSaleReceiptId?: string | null;
    /** DB persist time of receipt row. */
    receiptPersistedAtUtc?: string | null;
    hasOfflineOrigin?: boolean;
    offlineTransactionId?: string | null;
    offlineCreatedAtUtc?: string | null;
    fiscalizedAtUtc?: string | null;
    clockDriftWarning?: boolean;
    sequenceGapDetected?: boolean;
    sequenceDuplicateDetected?: boolean;
}
