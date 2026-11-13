import type { ReceiptDTO, ReceiptListItemDtoPagedResult } from '@/api/generated/model';
import {
  getApiReceiptsByPaymentPaymentId,
  getApiReceiptsList,
  getApiReceiptsReceiptId,
  getApiReceiptsReceiptIdSignatureDebug,
} from '@/api/generated/receipts/receipts';
import type {
  ReceiptDetailDto,
  ReceiptListItemDto,
  ReceiptListParams,
  ReceiptListResponse,
} from '@/features/receipts/types/receipts';
import type {
  PaymentSignatureDebugPayload,
  SignatureDebugApiResponse,
  SignatureDiagnosticStepDto,
} from '@/features/receipts/types/signature-debug';

function toListItem(
  row: NonNullable<ReceiptListItemDtoPagedResult['items']>[number]
): ReceiptListItemDto {
  return {
    receiptId: row.receiptId ?? '',
    paymentId: row.paymentId ?? '',
    receiptNumber: row.receiptNumber ?? '',
    issuedAt: row.issuedAt ?? '',
    cashierId: row.cashierId ?? null,
    cashRegisterId: row.cashRegisterId?.trim() ?? '',
    registerDisplayNumber: undefined,
    subTotal: row.subTotal ?? 0,
    taxTotal: row.taxTotal ?? 0,
    grandTotal: row.grandTotal ?? 0,
    createdAt: row.createdAt ?? '',
    rksvSpecialReceiptKind: row.rksvSpecialReceiptKind ?? null,
    rksvFinanzOnlineSubmissionStatus: row.rksvFinanzOnlineSubmissionStatus ?? null,
    hasStoredPdf: row.hasStoredPdf ?? false,
  };
}

/**
 * Maps Orval {@link ReceiptDTO} into {@link ReceiptDetailDto} (admin view model in `features/receipts/types`).
 * Contract rules: `cashRegisterId` stays the API machine/FK string; `kassenID` → `registerDisplayNumber` only
 * (display / RKSV text). Do not copy `kassenID` into `cashRegisterId` or infer register links from display fields.
 */
export function mapReceiptDtoToDetail(d: ReceiptDTO): ReceiptDetailDto {
  const issued = d.date ?? '';
  const persisted = d.receiptPersistedAtUtc ?? issued;
  const displayReg = d.kassenID?.trim() || undefined;
  return {
    receiptId: d.receiptId ?? '',
    paymentId: d.paymentId ?? null,
    receiptNumber: d.receiptNumber ?? '',
    issuedAt: issued,
    cashierId: d.cashierId ?? null,
    cashierDisplayName: d.cashierDisplayName ?? null,
    cashRegisterId: d.cashRegisterId?.trim() ?? '',
    registerDisplayNumber: displayReg,
    subTotal: d.subTotal ?? 0,
    taxTotal: d.taxAmount ?? 0,
    grandTotal: d.grandTotal ?? 0,
    qrCodePayload: d.signature?.qrData ?? null,
    signatureValue: d.signature?.signatureValue ?? null,
    prevSignatureValue: d.signature?.prevSignatureValue ?? null,
    createdAt: persisted,
    receiptPersistedAtUtc: persisted,
    hasOfflineOrigin: d.hasOfflineOrigin ?? false,
    offlineTransactionId: d.offlineTransactionId ?? null,
    offlineCreatedAtUtc: d.offlineCreatedAtUtc ?? null,
    fiscalizedAtUtc: d.fiscalizedAtUtc ?? null,
    clockDriftWarning: d.clockDriftWarning ?? false,
    sequenceGapDetected: d.sequenceGapDetected ?? false,
    sequenceDuplicateDetected: d.sequenceDuplicateDetected ?? false,
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
    fiscalTraceKind: d.fiscalTraceKind ?? null,
    originalPaymentId: d.originalPaymentId ?? null,
    originalSaleReceiptId: d.originalSaleReceiptId ?? null,
    rksvSpecialReceiptKind: d.rksvSpecialReceiptKind ?? null,
    rksvNullbelegActsAsJahresbeleg: d.rksvNullbelegActsAsJahresbeleg ?? false,
    rksvFinanzOnlineSubmission: d.rksvFinanzOnlineSubmission ?? null,
  };
}

export async function getReceiptListForensics(
  params: ReceiptListParams
): Promise<ReceiptListResponse> {
  const data = await getApiReceiptsList({
    page: params.page,
    pageSize: params.pageSize,
    sort: params.sort,
    receiptNumber: params.receiptNumber,
    cashRegisterId: params.cashRegisterId,
    cashierId: params.cashierId,
    issuedFrom: params.issuedFrom,
    issuedTo: params.issuedTo,
  });
  return {
    items: (data.items ?? []).map(toListItem),
    page: data.page ?? params.page,
    pageSize: data.pageSize ?? params.pageSize,
    totalCount: data.totalCount ?? 0,
  };
}

export async function getReceiptDetailForensics(receiptId: string): Promise<ReceiptDetailDto> {
  const data = await getApiReceiptsReceiptId(receiptId);
  return mapReceiptDtoToDetail(data);
}

export async function getReceiptByPaymentForensics(paymentId: string): Promise<ReceiptDetailDto> {
  const data = await getApiReceiptsByPaymentPaymentId(paymentId);
  return mapReceiptDtoToDetail(data);
}

/** OpenAPI returns `unknown` for this endpoint — narrow before reading optional keys (see RKSv_ADMIN_CONTRACT_GAPS.receiptSignatureDebugResponse). */
function isPlainJsonObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function normalizeReceiptSignaturePayload(raw: unknown): PaymentSignatureDebugPayload {
  if (!isPlainJsonObject(raw)) return { steps: [], compactJws: null };
  const o = raw;
  const verifyResult = typeof o.verifyResult === 'string' ? o.verifyResult.toUpperCase() : 'WARN';
  const hasSig = typeof o.signatureValue === 'string' && o.signatureValue.length > 0;
  const steps: SignatureDiagnosticStepDto[] = [
    {
      stepId: 1,
      name: 'Receipt signature present',
      status: hasSig ? 'PASS' : 'WARN',
      evidence: hasSig
        ? 'signatureValue present on receipt signature debug response.'
        : String(o.message ?? 'No signature'),
    },
    {
      stepId: 2,
      name: 'TSE signature verification',
      status:
        verifyResult === 'PASS'
          ? 'PASS'
          : verifyResult === 'FAIL'
            ? 'FAIL'
            : verifyResult === 'SIMULATED'
              ? 'SIMULATED'
              : 'WARN',
      evidence: `verifyResult=${verifyResult}`,
    },
  ];
  return {
    steps,
    compactJws: typeof o.signatureValue === 'string' ? o.signatureValue : null,
  };
}

export async function getReceiptSignatureDebugForensics(
  receiptId: string
): Promise<SignatureDebugApiResponse> {
  const raw = await getApiReceiptsReceiptIdSignatureDebug(receiptId);
  return {
    success: true,
    message: 'OK',
    data: normalizeReceiptSignaturePayload(raw),
    timestamp: new Date().toISOString(),
  };
}
