/**
 * Receipt admin view types. List/detail payloads are ultimately defined by Orval (`ReceiptListItemDto`, `ReceiptDTO`);
 * this file holds the **mapped** list row and detail shapes used by tables/cards (`mapReceiptDtoToDetail`, `toListItem`).
 * Do not treat these interfaces as a parallel OpenAPI contract — regenerate Orval when the backend changes.
 */

import type { RksvFinanzOnlineSubmissionStatusDto } from '@/api/generated/model';

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

/**
 * Single receipt in the paginated list (mapped from Orval ReceiptListItemDto).
 * Optional display register label is not in the current OpenAPI list item — reserved for a future field
 * (see RKSv_ADMIN_CONTRACT_GAPS.receiptListRegisterDisplay).
 */
export interface ReceiptListItemDto {
    receiptId: string;
    /** Zahlung für Nachdruck / by-payment Preview */
    paymentId?: string;
    receiptNumber: string;
    issuedAt: string;
    cashierId: string | null;
    /** Value from GET /api/Receipts/list cashRegisterId (may be empty if API omits it). */
    cashRegisterId: string;
    /** Populated only when backend adds a distinct display field to the list contract. */
    registerDisplayNumber?: string;
    subTotal: number;
    taxTotal: number;
    grandTotal: number;
    createdAt: string;
    /** RKSV Sonderbeleg marker from payment (e.g. Jahresbeleg, Monatsbeleg); null for normal sales. */
    rksvSpecialReceiptKind?: string | null;
    /** FinanzOnline/BMF lifecycle for Startbeleg/Jahresbeleg when a tracking row exists. */
    rksvFinanzOnlineSubmissionStatus?: string | null;
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

/**
 * Detail view model produced from generated `ReceiptDTO` (not a duplicate backend DTO).
 */
export interface ReceiptDetailDto {
    receiptId: string;
    paymentId: string | null;
    receiptNumber: string;
    issuedAt: string;
    cashierId: string | null;
    cashierDisplayName?: string | null;
    /** cash_registers.Id (UUID). */
    cashRegisterId: string;
    /** CashRegisters.RegisterNumber / receipt line display (not the FK). */
    registerDisplayNumber?: string;
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
    /** RKSV Sonderbeleg marker from payment; null for normal sales. */
    rksvSpecialReceiptKind?: string | null;
    /** When true, Nullbeleg row is flagged as annual-context (December flow). */
    rksvNullbelegActsAsJahresbeleg?: boolean;
    /** FinanzOnline/BMF submission snapshot for Startbeleg/Jahresbeleg (no secrets). */
    rksvFinanzOnlineSubmission?: RksvFinanzOnlineSubmissionStatusDto | null;
}
