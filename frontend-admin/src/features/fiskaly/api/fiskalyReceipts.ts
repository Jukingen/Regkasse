import { customInstance } from '@/lib/axios';
import { normalizeApiError } from '@/shared/errors/normalizedApiError';

export type FiskalyReceiptOperation =
  | 'normal'
  | 'cancel'
  | 'nullbeleg'
  | 'startbeleg'
  | 'monatsbeleg'
  | 'jahresbeleg'
  | 'schlussbeleg'
  | 'tagesabschluss';

export type FiskalyReceiptError = {
  code: string;
  message: string;
  details?: string | null;
};

export type FiskalyReceiptData = {
  receiptId: string;
  receiptNumber: string;
  signature?: string | null;
  qrCode?: string | null;
};

export type FiskalyReceiptEnvelope = {
  success: boolean;
  data?: FiskalyReceiptData | null;
  error?: FiskalyReceiptError | null;
  historyId?: string | null;
};

export type FiskalyReceiptRequestBody = {
  cashRegisterId: string;
  amount?: number;
  vatRate?: string;
  originalReceiptId?: string;
  reason?: string;
  year?: number;
  month?: number;
  closingId?: string;
  closingDate?: string;
};

function isRecord(v: unknown): v is Record<string, unknown> {
  return v != null && typeof v === 'object' && !Array.isArray(v);
}

export function parseFiskalyReceiptError(error: unknown): FiskalyReceiptError | null {
  const e = error as { response?: { data?: unknown } } | null;
  const data = e?.response?.data;
  if (isRecord(data) && isRecord(data.error)) {
    const code = typeof data.error.code === 'string' ? data.error.code : '';
    const message = typeof data.error.message === 'string' ? data.error.message : '';
    const details = typeof data.error.details === 'string' ? data.error.details : null;
    const topMessage = typeof data.message === 'string' ? data.message : '';
    if (code || message || topMessage) {
      return {
        code: code || 'FISKALY_API_ERROR',
        message: message || topMessage,
        details,
      };
    }
  }

  if (isRecord(data) && typeof data.message === 'string' && data.message.trim()) {
    const code = typeof data.code === 'string' ? data.code : 'FISKALY_API_ERROR';
    const details = typeof data.details === 'string' ? data.details : null;
    return { code, message: data.message, details };
  }

  const normalized = normalizeApiError(error);
  if (!normalized.code && !normalized.rawMessage) return null;
  return {
    code: normalized.code ?? 'FISKALY_API_ERROR',
    message: normalized.rawMessage ?? '',
    details: normalized.details ?? null,
  };
}

export async function postFiskalyTagesabschluss(
  closingId: string,
  signal?: AbortSignal
): Promise<FiskalyReceiptEnvelope> {
  try {
    return await customInstance<FiskalyReceiptEnvelope>({
      url: `/api/admin/fiskaly/receipt/tagesabschluss/${closingId}`,
      method: 'POST',
      signal,
    });
  } catch (err) {
    const parsed = parseFiskalyReceiptError(err);
    if (parsed) {
      return { success: false, error: parsed };
    }
    throw err;
  }
}

export async function postFiskalyReceiptOperation(
  operation: FiskalyReceiptOperation,
  body: FiskalyReceiptRequestBody,
  signal?: AbortSignal
): Promise<FiskalyReceiptEnvelope> {
  if (operation === 'tagesabschluss') {
    if (body.closingId) {
      return postFiskalyTagesabschluss(body.closingId, signal);
    }
    try {
      return await customInstance<FiskalyReceiptEnvelope>({
        url: '/api/admin/fiskaly/receipt/tagesabschluss',
        method: 'POST',
        data: {
          cashRegisterId: body.cashRegisterId,
          closingDate: body.closingDate,
        },
        signal,
      });
    } catch (err) {
      const parsed = parseFiskalyReceiptError(err);
      if (parsed) {
        return { success: false, error: parsed };
      }
      throw err;
    }
  }

  try {
    return await customInstance<FiskalyReceiptEnvelope>({
      url: `/api/admin/fiskaly/receipt/${operation}`,
      method: 'POST',
      data: body,
      signal,
    });
  } catch (err) {
    const parsed = parseFiskalyReceiptError(err);
    if (parsed) {
      return { success: false, error: parsed };
    }
    throw err;
  }
}
