import { apiClient } from './config';
import { unwrapApiResponseLayer, isRecord } from './normalizePosPaymentMethods';
import { posPaymentByIdPath } from './posPaymentPaths';
import type { ReceiptDTO } from '../../types/ReceiptDTO';

export type ReceiptListRow = {
  receiptId: string;
  paymentId: string;
  receiptNumber: string;
  grandTotal: number;
  cashRegisterId: string;
  cashRegisterEntityId?: string;
  rksvSpecialReceiptKind?: string | null;
};

function coerceUuid(raw: unknown): string {
  const s = String(raw ?? '').trim();
  return s;
}

/**
 * GET /api/Receipts/list — requires SaleView (Cashier has it).
 */
export async function searchReceiptsByReceiptNumber(params: {
  receiptNumber: string;
  cashRegisterId: string;
  pageSize?: number;
}): Promise<ReceiptListRow[]> {
  const raw = await apiClient.get<unknown>('/Receipts/list', {
    params: {
      page: 1,
      pageSize: params.pageSize ?? 15,
      receiptNumber: params.receiptNumber.trim(),
      cashRegisterId: params.cashRegisterId,
    },
  });

  const layer = unwrapApiResponseLayer(raw);
  const body = isRecord(layer) ? layer : isRecord(raw as object) ? (raw as Record<string, unknown>) : {};
  const itemsRaw = body.items ?? body.Items ?? [];
  if (!Array.isArray(itemsRaw)) return [];

  const rows: ReceiptListRow[] = [];
  for (const row of itemsRaw) {
    if (!isRecord(row)) continue;
    const receiptId = coerceUuid(row.receiptId ?? row.ReceiptId);
    const paymentId = coerceUuid(row.paymentId ?? row.PaymentId);
    const receiptNumber = String(row.receiptNumber ?? row.ReceiptNumber ?? '').trim();
    const grandTotal = Number(row.grandTotal ?? row.GrandTotal ?? 0);
    const cashRegisterId = coerceUuid(row.cashRegisterId ?? row.CashRegisterId ?? row.cashRegisterEntityId);
    if (!paymentId || !receiptNumber) continue;
    rows.push({
      receiptId,
      paymentId,
      receiptNumber,
      grandTotal: Number.isFinite(grandTotal) ? grandTotal : 0,
      cashRegisterId,
      cashRegisterEntityId: coerceUuid(row.cashRegisterEntityId ?? row.CashRegisterEntityId),
      rksvSpecialReceiptKind:
        row.rksvSpecialReceiptKind != null ? String(row.rksvSpecialReceiptKind) : (row.RksvSpecialReceiptKind as string | undefined) ?? null,
    });
  }
  return rows;
}

export type ParsedPaymentRow = {
  id: string;
  customerId: string;
  totalAmount: number;
  receiptNumber: string;
  cashRegisterId: string;
  isStorno?: boolean;
  isRefund?: boolean;
};

function extractPaymentObjectFromEnvelope(raw: unknown): Record<string, unknown> | null {
  const u = unwrapApiResponseLayer(raw);
  if (!isRecord(u)) return null;
  const direct = u.payment ?? u.Payment;
  if (isRecord(direct)) return direct;
  const d = u.data ?? u.Data;
  if (isRecord(d)) {
    const inner = d.payment ?? d.Payment;
    if (isRecord(inner)) return inner;
    if (d.id != null || d.Id != null) return d;
  }
  if (u.id != null || u.Id != null) return u;
  return null;
}

/** Extract payment row from GET /api/pos/payment/{id} SuccessResponse. */
export function parsePaymentRowFromGetResponse(raw: unknown): ParsedPaymentRow | null {
  const o = extractPaymentObjectFromEnvelope(raw);
  if (!o) return null;

  const id = coerceUuid(o.id ?? o.Id);
  const customerId = coerceUuid(o.customerId ?? o.CustomerId);
  const totalAmount = Number(o.totalAmount ?? o.TotalAmount ?? 0);
  const receiptNumber = String(o.receiptNumber ?? o.ReceiptNumber ?? '').trim();
  const cashRegisterId = coerceUuid(o.cashRegisterId ?? o.CashRegisterId);
  if (!id || !customerId || !cashRegisterId) return null;

  return {
    id,
    customerId,
    totalAmount: Number.isFinite(totalAmount) ? totalAmount : 0,
    receiptNumber,
    cashRegisterId,
    isStorno: !!(o.isStorno ?? o.IsStorno),
    isRefund: !!(o.isRefund ?? o.IsRefund),
  };
}

export async function fetchPaymentRowForPos(paymentId: string): Promise<ParsedPaymentRow | null> {
  try {
    const raw = await apiClient.get<unknown>(posPaymentByIdPath(paymentId));
    return parsePaymentRowFromGetResponse(raw);
  } catch {
    return null;
  }
}

export async function fetchReceiptDtoByPayment(paymentId: string): Promise<ReceiptDTO | null> {
  try {
    const raw = await apiClient.get<unknown>(`/Receipts/by-payment/${encodeURIComponent(paymentId)}`);
    const layer = unwrapApiResponseLayer(raw);
    const d = unwrapApiResponseLayer(layer);
    if (!d || typeof d !== 'object') return null;
    return d as ReceiptDTO;
  } catch {
    return null;
  }
}
