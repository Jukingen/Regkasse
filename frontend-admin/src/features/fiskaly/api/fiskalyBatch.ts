import { customInstance } from '@/lib/axios';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { parseFiskalyReceiptError, type FiskalyReceiptError } from '@/features/fiskaly/api/fiskalyReceipts';

export type FiskalyBatchLimits = {
  maxItems: number;
  warnAtItems: number;
};

export const DEFAULT_FISKALY_BATCH_LIMITS: FiskalyBatchLimits = {
  maxItems: 50,
  warnAtItems: 10,
};

export type FiskalyBatchItemResult = {
  key: string;
  success: boolean;
  historyId?: string | null;
  label?: string | null;
  error?: FiskalyReceiptError | null;
};

export type FiskalyBatchOperationResult = {
  batchId: string;
  total: number;
  successCount: number;
  failedCount: number;
  results: FiskalyBatchItemResult[];
};

export type FiskalyBatchProgressEvent = {
  batchId: string;
  tenantId?: string | null;
  kind: string;
  current: number;
  total: number;
  currentLabel?: string | null;
  successCount: number;
  failedCount: number;
  done: boolean;
};

export type FiskalyBatchStornoItem = {
  cashRegisterId: string;
  originalReceiptId: string;
  receiptNumber?: string;
};

export type FiskalyBatchSonderbelegKind = 'startbeleg' | 'monatsbeleg' | 'jahresbeleg';

export type FiskalyBatchRequestError = FiskalyReceiptError & { maxItems?: number };

const BASE = '/api/admin/fiskaly/batch';

function isRecord(value: unknown): value is Record<string, unknown> {
  return value != null && typeof value === 'object' && !Array.isArray(value);
}

export function clampFiskalyBatchLimits(input?: Partial<FiskalyBatchLimits> | null): FiskalyBatchLimits {
  const maxRaw = input?.maxItems ?? DEFAULT_FISKALY_BATCH_LIMITS.maxItems;
  const maxItems = Math.min(50, Math.max(1, maxRaw <= 0 ? 50 : maxRaw));
  const warnRaw = input?.warnAtItems ?? DEFAULT_FISKALY_BATCH_LIMITS.warnAtItems;
  const warnAtItems = Math.min(maxItems, Math.max(1, warnRaw <= 0 ? 10 : warnRaw));
  return { maxItems, warnAtItems };
}

export function createFiskalyBatchId(): string {
  return globalThis.crypto?.randomUUID?.() ?? `batch-${Date.now()}`;
}

export function isFiskalyStornoEligible(row: {
  paymentId?: string | null;
  cashRegisterId?: string | null;
  rksvSpecialReceiptKind?: string | null;
}): boolean {
  return Boolean(row.paymentId?.trim()) && Boolean(row.cashRegisterId?.trim()) && !row.rksvSpecialReceiptKind;
}

export function toFiskalyBatchStornoItems(
  rows: Array<{
    paymentId?: string | null;
    cashRegisterId?: string | null;
    receiptNumber?: string | null;
    rksvSpecialReceiptKind?: string | null;
  }>
): FiskalyBatchStornoItem[] {
  const seen = new Set<string>();
  const items: FiskalyBatchStornoItem[] = [];
  for (const row of rows) {
    if (!isFiskalyStornoEligible(row) || !row.paymentId || !row.cashRegisterId) continue;
    const originalReceiptId = row.paymentId.trim();
    if (seen.has(originalReceiptId)) continue;
    seen.add(originalReceiptId);
    items.push({
      cashRegisterId: row.cashRegisterId.trim(),
      originalReceiptId,
      receiptNumber: row.receiptNumber?.trim() || undefined,
    });
  }
  return items;
}

export function asFiskalyBatchProgressEvent(payload: unknown): FiskalyBatchProgressEvent | null {
  if (!isRecord(payload)) return null;
  if (typeof payload.batchId !== 'string' && typeof payload.batchId !== 'number') return null;
  return {
    batchId: String(payload.batchId),
    tenantId: typeof payload.tenantId === 'string' ? payload.tenantId : null,
    kind: String(payload.kind ?? ''),
    current: typeof payload.current === 'number' ? payload.current : 0,
    total: typeof payload.total === 'number' ? payload.total : 0,
    currentLabel: typeof payload.currentLabel === 'string' ? payload.currentLabel : null,
    successCount: typeof payload.successCount === 'number' ? payload.successCount : 0,
    failedCount: typeof payload.failedCount === 'number' ? payload.failedCount : 0,
    done: Boolean(payload.done),
  };
}

export function parseFiskalyBatchError(error: unknown): FiskalyBatchRequestError | null {
  const data = (error as { response?: { data?: unknown } } | null)?.response?.data;
  if (isRecord(data) && typeof data.code === 'string') {
    return {
      code: data.code,
      message: typeof data.message === 'string' ? data.message : '',
      maxItems: typeof data.maxItems === 'number' ? data.maxItems : undefined,
    };
  }
  return parseFiskalyReceiptError(error);
}

async function rethrowBlobError(error: unknown): Promise<never> {
  const data = (error as { response?: { data?: unknown } } | null)?.response?.data;
  if (data instanceof Blob) {
    const text = await data.text();
    try {
      const json: unknown = JSON.parse(text);
      throw Object.assign(new Error(isRecord(json) && typeof json.message === 'string' ? json.message : text), {
        response: { data: json },
      });
    } catch (parsed) {
      if (parsed instanceof SyntaxError) throw error;
      throw parsed;
    }
  }
  throw error;
}

export async function getFiskalyBatchLimits(signal?: AbortSignal): Promise<FiskalyBatchLimits> {
  const raw = await customInstance<FiskalyBatchLimits>({ url: `${BASE}/limits`, method: 'GET', signal });
  return clampFiskalyBatchLimits(raw);
}

export async function postFiskalyBatchStorno(
  body: { batchId?: string; items: FiskalyBatchStornoItem[]; reason: string },
  signal?: AbortSignal
): Promise<FiskalyBatchOperationResult> {
  return customInstance<FiskalyBatchOperationResult>({
    url: `${BASE}/storno`,
    method: 'POST',
    data: body,
    signal,
  });
}

export async function postFiskalyBatchSonderbelege(
  body: {
    batchId?: string;
    kind: FiskalyBatchSonderbelegKind;
    cashRegisterIds: string[];
    year?: number;
    month?: number;
    reason?: string;
  },
  signal?: AbortSignal
): Promise<FiskalyBatchOperationResult> {
  return customInstance<FiskalyBatchOperationResult>({
    url: `${BASE}/sonderbelege`,
    method: 'POST',
    data: body,
    signal,
  });
}

export type FiskalyBatchDepExportZip = {
  blob: Blob;
  fileName: string;
  successCount: number;
  failedCount: number;
};

export async function postFiskalyBatchDepExport(
  body: {
    batchId?: string;
    tenantIds: string[];
    fromUtc: string;
    toUtc: string;
    includeSpecialReceipts?: boolean;
    includeDailyClosings?: boolean;
  },
  signal?: AbortSignal
): Promise<FiskalyBatchDepExportZip> {
  try {
    const response = await AXIOS_INSTANCE.post(`${BASE}/dep-export`, body, {
      responseType: 'blob',
      signal,
    });
    const blob = response.data as Blob;
    const disposition = String(response.headers['content-disposition'] ?? '');
    const match = /filename="?([^"]+)"?/i.exec(disposition);
    const fileName = match?.[1] ?? 'dep-export-batch.zip';
    return {
      blob,
      fileName,
      successCount: Number(response.headers['x-regkasse-batch-success'] ?? 0),
      failedCount: Number(response.headers['x-regkasse-batch-failed'] ?? 0),
    };
  } catch (err) {
    await rethrowBlobError(err);
  }
}
