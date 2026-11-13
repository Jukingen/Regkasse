import { API_PATHS } from './apiPaths';
import { apiClient } from './config';
import { unwrapApiResponseLayer, isRecord } from './normalizePosPaymentMethods';
import type { ReceiptDTO } from '../../types/ReceiptDTO';

export const POS_RECEIPTS_RECENT_LIMIT = 20;

export const POS_RECEIPT_REPRINT_REASONS = {
  CUSTOMER_REQUEST: 'CUSTOMER_REQUEST',
  PRINTER_FAILURE: 'PRINTER_FAILURE',
} as const;

export type PosReceiptReprintReason =
  (typeof POS_RECEIPT_REPRINT_REASONS)[keyof typeof POS_RECEIPT_REPRINT_REASONS];

export type PosReceiptListItem = {
  receiptId: string;
  paymentId: string;
  receiptNumber: string;
  issuedAt: string;
  grandTotal: number;
  cashRegisterId: string;
  cashierId?: string;
  status: string;
};

export type PosReceiptCancelResult = {
  success: boolean;
  errorKey?: string | null;
  messageKey?: string | null;
  stornoPaymentId?: string | null;
  diagnosticCode?: string | null;
  requiresApproval?: boolean;
};

export type PosReceiptReprintResult = {
  outcome: string;
  paymentId: string;
  receiptId: string;
  receiptNumber: string;
  receipt: ReceiptDTO | null;
};

function coerceUuid(raw: unknown): string {
  return String(raw ?? '').trim();
}

function coerceIsoDate(raw: unknown): string {
  if (raw instanceof Date && !Number.isNaN(raw.getTime())) return raw.toISOString();
  const s = String(raw ?? '').trim();
  return s;
}

export function parsePosReceiptListItem(raw: unknown): PosReceiptListItem | null {
  if (!isRecord(raw)) return null;
  const receiptId = coerceUuid(raw.receiptId ?? raw.ReceiptId);
  const paymentId = coerceUuid(raw.paymentId ?? raw.PaymentId);
  const receiptNumber = String(raw.receiptNumber ?? raw.ReceiptNumber ?? '').trim();
  const issuedAt = coerceIsoDate(raw.issuedAt ?? raw.IssuedAt ?? raw.date ?? raw.Date ?? raw.createdAt);
  const grandTotal = Number(raw.grandTotal ?? raw.GrandTotal ?? 0);
  const cashRegisterId = coerceUuid(
    raw.cashRegisterEntityId ?? raw.CashRegisterEntityId ?? raw.cashRegisterId ?? raw.CashRegisterId
  );
  const status = String(raw.status ?? raw.Status ?? '').trim() || 'Paid';
  const cashierId = String(raw.cashierId ?? raw.CashierId ?? '').trim();
  if (!receiptId || !paymentId || !receiptNumber) return null;
  return {
    receiptId,
    paymentId,
    receiptNumber,
    issuedAt,
    grandTotal: Number.isFinite(grandTotal) ? grandTotal : 0,
    cashRegisterId,
    cashierId: cashierId || undefined,
    status,
  };
}

export function parsePosReceiptList(raw: unknown): PosReceiptListItem[] {
  const layer = unwrapApiResponseLayer(raw);
  const body = isRecord(layer) ? layer : isRecord(raw) ? raw : {};
  const itemsRaw = body.items ?? body.Items ?? [];
  if (!Array.isArray(itemsRaw)) return [];
  const rows: PosReceiptListItem[] = [];
  for (const row of itemsRaw) {
    const item = parsePosReceiptListItem(row);
    if (item) rows.push(item);
  }
  return rows;
}

function posReceiptsPath(receiptId?: string, reprint = false): string {
  if (!receiptId) return API_PATHS.POS_RECEIPTS.RECENT;
  return reprint ? API_PATHS.POS_RECEIPTS.REPRINT(receiptId) : API_PATHS.POS_RECEIPTS.BY_ID(receiptId);
}

export async function fetchRecentReceipts(params: {
  cashRegisterId: string;
  pageSize?: number;
}): Promise<PosReceiptListItem[]> {
  const limit = Math.min(
    Math.max(params.pageSize ?? POS_RECEIPTS_RECENT_LIMIT, 1),
    POS_RECEIPTS_RECENT_LIMIT
  );
  const raw = await apiClient.get<unknown>(posReceiptsPath(), {
    params: {
      cashRegisterId: params.cashRegisterId,
      limit,
    },
  });
  return parsePosReceiptList(raw);
}

export async function fetchReceiptById(params: {
  receiptId: string;
  cashRegisterId: string;
}): Promise<ReceiptDTO | null> {
  try {
    const raw = await apiClient.get<unknown>(posReceiptsPath(params.receiptId), {
      params: { cashRegisterId: params.cashRegisterId },
    });
    const layer = unwrapApiResponseLayer(raw);
    const d = unwrapApiResponseLayer(layer);
    if (!d || typeof d !== 'object') return null;
    return d as ReceiptDTO;
  } catch {
    return null;
  }
}

export function parsePosReceiptReprint(raw: unknown): PosReceiptReprintResult | null {
  const layer = unwrapApiResponseLayer(raw);
  const body = isRecord(layer) ? layer : isRecord(raw) ? raw : {};
  const receiptRaw = body.receipt ?? body.Receipt ?? body;
  if (!isRecord(receiptRaw)) return null;
  const paymentId = coerceUuid(receiptRaw.paymentId ?? receiptRaw.PaymentId);
  const receiptId = coerceUuid(receiptRaw.receiptId ?? receiptRaw.ReceiptId);
  if (!paymentId && !receiptId) return null;
  return {
    outcome: String(body.outcome ?? body.Outcome ?? 'Success'),
    paymentId,
    receiptId,
    receiptNumber: String(receiptRaw.receiptNumber ?? receiptRaw.ReceiptNumber ?? '').trim(),
    receipt: receiptRaw as unknown as ReceiptDTO,
  };
}

/**
 * GET /api/pos/receipts/{receiptId}/reprint — persisted receipt only (no new fiscal Beleg).
 * `receiptId` may be the receipt GUID or the payment GUID.
 */
export async function reprintReceipt(params: {
  receiptId: string;
  cashRegisterId: string;
  reasonCode?: PosReceiptReprintReason;
}): Promise<PosReceiptReprintResult> {
  const raw = await apiClient.get<unknown>(posReceiptsPath(params.receiptId, true), {
    params: {
      cashRegisterId: params.cashRegisterId,
      reasonCode: params.reasonCode ?? POS_RECEIPT_REPRINT_REASONS.CUSTOMER_REQUEST,
    },
  });
  const parsed = parsePosReceiptReprint(raw);
  if (!parsed?.paymentId) {
    throw new Error('Reprint did not return a payment id');
  }
  return parsed;
}

function parsePosReceiptCancel(raw: unknown): PosReceiptCancelResult {
  const layer = unwrapApiResponseLayer(raw);
  const body = isRecord(layer) ? layer : isRecord(raw) ? raw : {};
  return {
    success: Boolean(body.success ?? body.Success),
    errorKey: typeof (body.errorKey ?? body.ErrorKey) === 'string' ? String(body.errorKey ?? body.ErrorKey) : null,
    messageKey:
      typeof (body.messageKey ?? body.MessageKey) === 'string' ? String(body.messageKey ?? body.MessageKey) : null,
    stornoPaymentId: coerceUuid(body.stornoPaymentId ?? body.StornoPaymentId) || null,
    diagnosticCode:
      typeof (body.diagnosticCode ?? body.DiagnosticCode) === 'string'
        ? String(body.diagnosticCode ?? body.DiagnosticCode)
        : null,
    requiresApproval: Boolean(body.requiresApproval ?? body.RequiresApproval),
  };
}

/**
 * POST /api/pos/receipts/{receiptId}/cancel — full fiscal storno (Fiskaly CANCELLATION).
 */
export async function cancelReceipt(params: {
  receiptId: string;
  cashRegisterId: string;
  reason?: string;
}): Promise<PosReceiptCancelResult> {
  try {
    const raw = await apiClient.post<unknown>(API_PATHS.POS_RECEIPTS.CANCEL(params.receiptId), {
      reason: params.reason?.trim() || undefined,
    }, {
      params: { cashRegisterId: params.cashRegisterId },
    });
    return parsePosReceiptCancel(raw);
  } catch (err) {
    const record = isRecord(err) ? err : null;
    const data = record?.data ?? (isRecord(record?.response) ? record.response.data : undefined);
    if (data) {
      const parsed = parsePosReceiptCancel(data);
      if (parsed.errorKey || parsed.diagnosticCode) return parsed;
    }
    throw err;
  }
}
