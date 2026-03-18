import { useQuery } from '@tanstack/react-query';
import { customInstance } from '@/lib/axios';
import type { ReceiptDetailDto } from '@/features/receipts/types/receipts';
import type { ReceiptDTO } from '@/api/generated/model';
import { RECEIPT_KEYS } from './useReceiptListQuery';

// ---------------------------------------------------------------------------
// Map API ReceiptDTO to ReceiptDetailDto (backend uses different field names)
// ---------------------------------------------------------------------------

function mapReceiptDtoToDetail(d: ReceiptDTO): ReceiptDetailDto {
    const ext = d as ReceiptDTO & {
        paymentId?: string;
        fiscalTraceKind?: string | null;
        originalPaymentId?: string | null;
        originalSaleReceiptId?: string | null;
        cashRegisterId?: string | null;
        receiptPersistedAtUtc?: string;
        hasOfflineOrigin?: boolean;
        offlineTransactionId?: string | null;
        offlineCreatedAtUtc?: string | null;
        fiscalizedAtUtc?: string | null;
        clockDriftWarning?: boolean;
        sequenceGapDetected?: boolean;
        sequenceDuplicateDetected?: boolean;
    };
    const issued = d.date ?? '';
    const persisted = ext.receiptPersistedAtUtc ?? issued;
    return {
        receiptId: d.receiptId ?? '',
        paymentId: ext.paymentId ?? null,
        receiptNumber: d.receiptNumber ?? '',
        issuedAt: issued,
        cashierId: d.cashierName ?? null,
        cashRegisterId: d.kassenID ?? '',
        subTotal: d.subTotal ?? 0,
        taxTotal: d.taxAmount ?? 0,
        grandTotal: d.grandTotal ?? 0,
        qrCodePayload: d.signature?.qrData ?? null,
        signatureValue: d.signature?.signatureValue ?? null,
        prevSignatureValue: d.signature?.prevSignatureValue ?? null,
        createdAt: persisted,
        receiptPersistedAtUtc: persisted,
        hasOfflineOrigin: ext.hasOfflineOrigin ?? false,
        offlineTransactionId: ext.offlineTransactionId ?? null,
        offlineCreatedAtUtc: ext.offlineCreatedAtUtc ?? null,
        fiscalizedAtUtc: ext.fiscalizedAtUtc ?? null,
        clockDriftWarning: ext.clockDriftWarning ?? false,
        sequenceGapDetected: ext.sequenceGapDetected ?? false,
        sequenceDuplicateDetected: ext.sequenceDuplicateDetected ?? false,
        items: (d.items ?? []).map((i) => ({
            itemId: i.itemId ?? '',
            productName: i.name ?? '',
            quantity: i.quantity ?? 0,
            unitPrice: i.unitPrice ?? 0,
            totalPrice: i.totalPrice ?? 0,
            taxRate: i.taxRate ?? 0,
        })),
        taxLines: (d.taxRates ?? []).map((t, idx) => ({
            lineId: `line-${idx}`,
            taxRate: t.rate ?? 0,
            netAmount: t.netAmount ?? 0,
            taxAmount: t.taxAmount ?? 0,
            grossAmount: t.grossAmount ?? 0,
        })),
        fiscalTraceKind: ext.fiscalTraceKind ?? null,
        originalPaymentId: ext.originalPaymentId ?? null,
        originalSaleReceiptId: ext.originalSaleReceiptId ?? null,
    };
}

// ---------------------------------------------------------------------------
// Fetcher (uses backend GET /api/Receipts/{id}, then maps to detail shape)
// ---------------------------------------------------------------------------

async function fetchReceiptDetail(receiptId: string): Promise<ReceiptDetailDto> {
    const data = await customInstance<ReceiptDTO>({
        url: `/api/Receipts/${receiptId}`,
        method: 'GET',
    });
    return mapReceiptDtoToDetail(data);
}

// ---------------------------------------------------------------------------
// Hook
// ---------------------------------------------------------------------------

/**
 * Fetches a single receipt by ID (includes items + taxLines).
 * Disabled when receiptId is falsy.
 */
export function useReceiptDetailQuery(receiptId: string | undefined) {
    return useQuery({
        queryKey: RECEIPT_KEYS.detail(receiptId!),
        queryFn: () => fetchReceiptDetail(receiptId!),
        enabled: !!receiptId,
        staleTime: 30_000,
    });
}
