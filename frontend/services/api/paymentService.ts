import { Buffer } from 'buffer';
import { apiClient, API_BASE_URL } from './config';
import { sessionManager } from '../session/sessionManager';
import {
  POS_PAYMENT_API_PREFIX,
  POS_PAYMENT_METHODS_PATH,
  posPaymentByIdPath,
  posPaymentQrPngAbsoluteUrl,
} from './posPaymentPaths';
import {
  isRecord,
  normalizeToPosPaymentMethods,
  type NormalizedPosPaymentMethod,
  unwrapApiResponseLayer,
} from './normalizePosPaymentMethods';
import { normalizePaymentError } from '../../features/payment/paymentErrors';
import { normalizePosPaymentItemsForRequest } from '../../utils/paymentTaxType';
import {
  enqueuePendingPayment,
  syncPendingPaymentQueue as flushPendingPaymentQueue,
  removePendingByIdempotencyKey,
  getPendingPaymentQueue,
  type PendingPaymentPayload,
} from '../payment/pendingPaymentQueue';
import { debugPosPaymentTrace } from '../../utils/debugPosPaymentTrace';
import type { CustomerKind } from '../../types/customerKind';

export type { PendingPaymentEntry } from '../payment/pendingPaymentQueue';

/** Same row shape as `normalizeToPosPaymentMethods` — single source of truth. */
export type PaymentMethod = NormalizedPosPaymentMethod;

/** String sent in `payment.method` on POST /api/pos/payment — stable code from admin / GET methods. */
export type PosPaymentMethodCode = string;

// Backend PaymentItemRequest: one item per cart line. Phase D: POS does not send modifierIds; add-ons are separate lines.
export interface PaymentItem {
  productId: string;
  quantity: number;
  taxType: 'standard' | 'reduced' | 'special';
}

// Backend'deki CreatePaymentRequest ile uyumlu
export interface PaymentRequest {
  customerId: string; // Guid string formatında (00000000-0000-0000-0000-000000000000)
  items: PaymentItem[];
  payment: {
    method: string;
    tseRequired: boolean;
    amount?: number; // Opsiyonel
  };
  tableNumber: number;
  totalAmount: number;

  steuernummer?: string;
  /** Required: cash register row UUID from user settings (Kasse). */
  cashRegisterId: string;

  notes?: string;

  /** Optional idempotency key for this payment attempt. Same key on retry returns existing payment. */
  idempotencyKey?: string;

  /** Optional explicit customer classification (server infers when omitted). */
  customerKind?: CustomerKind;
}

/** Backend'den gelen TSE/QR bilgisi - payment.tse */
export interface PaymentTseInfo {
  qrPayload?: string;
  isDemoFiscal?: boolean;
  provider?: string;
  receiptNumber?: string;
}

/** FISCAL_COMPLETE = server confirmed; NON_FISCAL_PENDING = queued locally; FAILED = server error response or replay failure. */
export type FiscalPaymentStatus =
  | 'FISCAL_COMPLETE'
  | 'NON_FISCAL_PENDING'
  | 'FAILED';

export interface PaymentResponse {
  success: boolean;
  /** True only after server accepted payment (fiscal path). */
  isSynced: boolean;
  fiscalStatus: FiscalPaymentStatus;
  /** Set when fiscalStatus is NON_FISCAL_PENDING */
  pendingQueueId?: string;
  paymentId: string;
  error?: string;
  message?: string;
  tseSignature?: string;
  /** TSE / QR info from POST /api/pos/payment response */
  tse?: PaymentTseInfo;
  /** When false, payment succeeded but invoice was not persisted — operator attention required. */
  invoicePersisted?: boolean;
}

function isTransportFailure(error: unknown): boolean {
  const err = error as {
    response?: unknown;
    message?: string;
    code?: string;
  };
  const msg = typeof err?.message === 'string' ? err.message : '';
  if (msg.includes('Token expired')) return false;
  if (err?.response != null) return false;
  if (err?.code === 'ECONNABORTED' || err?.code === 'ERR_NETWORK') return true;
  if (msg === 'Network Error' || /network/i.test(msg)) return true;
  if (/aborted|timeout/i.test(msg)) return true;
  return false;
}

export interface Receipt {
  id: string;
  receiptNumber: string;
  items: {
    productName: string;
    quantity: number;
    price: number;
    taxType: string;
    totalPrice: number;
  }[];
  subtotal: number;
  taxStandard: number;
  taxReduced: number;
  taxSpecial: number;
  total: number;
  paymentMethod: string;
  timestamp: string;
  cashierId: string;
}

class PaymentService {
  /** All payment HTTP uses `/api/pos/payment` — see `posPaymentPaths.ts` (canonical POS boundary). */
  private readonly baseUrl = POS_PAYMENT_API_PREFIX;

  async getPaymentMethods(): Promise<PaymentMethod[]> {
    const raw = await apiClient.get<unknown>(POS_PAYMENT_METHODS_PATH);
    const list = normalizeToPosPaymentMethods(raw);
    if (list.length === 0 && raw != null) {
      console.warn('[paymentService] No payment methods parsed from response:', raw);
    }
    return list as PaymentMethod[];
  }

  // Payment processing - Backend endpoint compatible
  /** Normalizes `items[*].taxType` (shared helper) before POST and before offline enqueue. */
  async processPayment(paymentRequest: PaymentRequest): Promise<PaymentResponse> {
    const idempotencyKey =
      paymentRequest.idempotencyKey?.trim() ||
      (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
        ? crypto.randomUUID()
        : `${Date.now()}-${Math.random().toString(36).slice(2, 15)}`);
    const items = normalizePosPaymentItemsForRequest(paymentRequest.items);
    const req: PaymentRequest = { ...paymentRequest, idempotencyKey, items };

    debugPosPaymentTrace('process_payment_enter', {
      cashRegisterId: req.cashRegisterId,
      itemCount: req.items?.length ?? 0,
      method: req.payment?.method,
      idempotencyKey,
    });

    try {
      debugPosPaymentTrace('payment_api_request_start', { path: this.baseUrl });
      const response = await apiClient.post<any>(`${this.baseUrl}`, req);
      const normalized = this.normalizePaymentResponse(response);
      if (normalized.success) {
        debugPosPaymentTrace('payment_api_success', {
          paymentId: normalized.paymentId,
          fiscalStatus: normalized.fiscalStatus,
        });
        await removePendingByIdempotencyKey(idempotencyKey);
      } else {
        debugPosPaymentTrace('payment_api_error', {
          fiscalStatus: normalized.fiscalStatus,
          message: normalized.message,
          error: normalized.error,
        });
      }
      return normalized;
    } catch (error: unknown) {
      if (isTransportFailure(error)) {
        debugPosPaymentTrace('payment_api_transport_failure_queue', {
          message: error instanceof Error ? error.message : String(error),
        });
        const payload = req as unknown as PendingPaymentPayload;
        const pendingQueueId = await enqueuePendingPayment(payload);
        return {
          success: false,
          isSynced: false,
          fiscalStatus: 'NON_FISCAL_PENDING',
          pendingQueueId,
          paymentId: '',
          error: 'NON_FISCAL_PENDING',
          message:
            'Not fiscal: no server confirmation. Payment stored in pending queue only.',
          invoicePersisted: false,
        };
      }
      debugPosPaymentTrace('payment_api_error_throw', {
        message: error instanceof Error ? error.message : String(error),
      });
      throw normalizePaymentError(error);
    }
  }

  // Helper to handle inconsistent backend responses (e.g. nested Value object)
  private normalizePaymentResponse(response: any): PaymentResponse {
    // 1. Unwrap "Value" if present (ASP.NET Core ActionResult serialization issue)
    const raw = response?.Value ? response.Value : response;

    // 2. Normalize Success
    // Check top-level success, data.Success, or Value.success
    const success =
      raw?.success === true ||
      raw?.Success === true ||
      raw?.data?.Success === true ||
      raw?.data?.success === true ||
      raw?.Value?.success === true ||
      raw?.Value?.Success === true;

    // 3. Normalize PaymentId (flat body, SuccessResponse data, Value wrapper, nested payment object)
    const tryExtractPaymentId = (o: unknown): string => {
      if (!o || typeof o !== 'object') return '';
      const r = o as Record<string, unknown>;
      const direct = r.paymentId ?? r.PaymentId;
      if (direct !== undefined && direct !== null && direct !== '') return String(direct);
      const pay = (r.payment ?? r.Payment) as Record<string, unknown> | undefined;
      if (pay && typeof pay === 'object') {
        const id = pay.id ?? pay.Id;
        if (id !== undefined && id !== null && id !== '') return String(id);
      }
      return '';
    };
    const paymentId =
      tryExtractPaymentId(raw) ||
      tryExtractPaymentId((raw as any)?.Value) ||
      tryExtractPaymentId((raw as any)?.data) ||
      tryExtractPaymentId((raw as any)?.Data) ||
      '';

    // 4. Normalize Message
    const message =
      raw?.message ||
      raw?.Message ||
      raw?.data?.Message ||
      (success ? 'Payment successful' : 'Payment failed');

    // 5. Normalize TseSignature
    const tseSignature =
      raw?.tseSignature ||
      raw?.TseSignature ||
      raw?.payment?.tseSignature ||
      raw?.data?.Payment?.TseSignature;

    // 6. Normalize TSE info (qrPayload, isDemoFiscal, provider)
    const rawTse =
      raw?.tse ||
      raw?.Tse ||
      raw?.data?.tse ||
      raw?.data?.Tse ||
      raw?.Value?.tse ||
      raw?.Value?.Tse;
    const tse: PaymentTseInfo | undefined = rawTse
      ? {
          qrPayload: rawTse.qrPayload ?? rawTse.QrPayload,
          isDemoFiscal: rawTse.isDemoFiscal ?? rawTse.IsDemoFiscal,
          provider: rawTse.provider ?? rawTse.Provider,
          receiptNumber: rawTse.receiptNumber ?? rawTse.ReceiptNumber
        }
      : undefined;

    const invoicePersisted = raw?.invoicePersisted !== false;

    return {
      success: !!success,
      isSynced: !!success,
      fiscalStatus: success ? 'FISCAL_COMPLETE' : 'FAILED',
      paymentId,
      message,
      tseSignature,
      tse,
      invoicePersisted,
      error: success ? undefined : message,
    };
  }

  /** Loads persisted fiscal receipt from GET /Receipts/by-payment/{paymentId}. */
  async createReceipt(paymentId: string): Promise<Receipt> {
    try {
      const d = await apiClient.get<{
        receiptId?: string;
        receiptNumber?: string;
        paymentId?: string;
        items?: Array<{ name?: string; quantity?: number; unitPrice?: number; taxType?: string; totalPrice?: number }>;
        subTotal?: number;
        taxAmount?: number;
        grandTotal?: number;
        payments?: Array<{ method?: string }>;
        date?: string;
        cashierId?: string;
        CashierId?: string;
        cashierDisplayName?: string;
        CashierDisplayName?: string;
        taxRates?: Array<{ taxAmount?: number; rate?: number }>;
      }>(`/Receipts/by-payment/${paymentId}`);

      const std = d.taxRates?.find((t) => t.rate === 20 || t.rate === 20.0)?.taxAmount ?? 0;
      const red = d.taxRates?.find((t) => t.rate === 10 || t.rate === 10.0)?.taxAmount ?? 0;
      const spec = d.taxRates?.find((t) => t.rate === 13 || t.rate === 13.0)?.taxAmount ?? 0;

      return {
        id: d.receiptId ?? paymentId,
        receiptNumber: d.receiptNumber ?? paymentId,
        items: (d.items ?? []).map((i) => ({
          productName: i.name ?? '',
          quantity: i.quantity ?? 0,
          price: i.unitPrice ?? 0,
          taxType: String(i.taxType ?? ''),
          totalPrice: i.totalPrice ?? 0
        })),
        subtotal: d.subTotal ?? 0,
        taxStandard: std,
        taxReduced: red,
        taxSpecial: spec,
        total: d.grandTotal ?? 0,
        paymentMethod: d.payments?.[0]?.method ?? 'cash',
        timestamp: d.date ?? new Date().toISOString(),
        cashierId:
          d.cashierId ??
          d.CashierId ??
          ''
      };
    } catch (e) {
      console.error('[Receipt] Persisted receipt not available:', e);
      throw new Error(
        'Kein fiscal hinterlegter Beleg (Server). Bitte Verbindung prüfen oder Zahlung erneut abrufen.'
      );
    }
  }

  /**
   * RKSV fiş için QR PNG'yi base64 data URL olarak getirir.
   * Backend GET /api/pos/payment/{id}/qr.png.
   * CORS/offline sorunlarını önlemek için print template'e data URL gömülür.
   */
  async getQrPngAsBase64(paymentId: string): Promise<string | null> {
    try {
      const token = await sessionManager.getAccessToken();
      const url = posPaymentQrPngAbsoluteUrl(API_BASE_URL, paymentId);
      const res = await fetch(url, {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      });
      if (!res.ok) {
        console.warn('[PaymentService] QR fetch failed:', res.status, res.statusText);
        return null;
      }
      const arrayBuffer = await res.arrayBuffer();
      const base64 = Buffer.from(arrayBuffer).toString('base64');
      return `data:image/png;base64,${base64}`;
    } catch (e) {
      console.warn('[PaymentService] QR fetch error:', e);
      return null;
    }
  }

  // Get receipt data for printing
  async getReceipt(paymentId: string): Promise<any> {
    try {
      const response = await apiClient.get<unknown>(posPaymentByIdPath(paymentId, 'receipt'));
      return unwrapApiResponseLayer(response);
    } catch (error) {
      console.error('Receipt fetch failed:', error);
      throw error;
    }
  }

  // Ödeme geçmişi
  async getPaymentHistory(limit: number = 50, offset: number = 0): Promise<PaymentResponse[]> {
    try {
      const response = await apiClient.get<PaymentResponse[]>(
        `${this.baseUrl}/history?limit=${encodeURIComponent(String(limit))}&offset=${encodeURIComponent(String(offset))}`
      );
      return response;
    } catch (error) {
      console.error('Payment history fetch failed:', error);
      return [];
    }
  }

  // Belirli bir ödeme
  async getPaymentById(id: string): Promise<PaymentResponse> {
    try {
      const response = await apiClient.get<PaymentResponse>(posPaymentByIdPath(id));
      return response;
    } catch (error) {
      console.error('Payment fetch failed:', error);
      return {
        success: false,
        isSynced: false,
        fiscalStatus: 'FAILED',
        paymentId: '',
        error: 'Payment not found',
      };
    }
  }

  // Ödeme iptal
  async cancelPayment(paymentId: string, reason?: string): Promise<any> {
    try {
      const response = await apiClient.post<any>(posPaymentByIdPath(paymentId, 'cancel'), {
        reason: reason || 'Kasiyer tarafından iptal edildi'
      });
      return response;
    } catch (error) {
      console.error('Payment cancellation failed:', error);
      throw new Error(
        'Storno erfordert Serververbindung (RKSV). Bitte online erneut versuchen.'
      );
    }
  }

  // Ödeme iade
  async refundPayment(id: string, amount: number, reason: string): Promise<PaymentResponse> {
    try {
      const response = await apiClient.post<PaymentResponse>(posPaymentByIdPath(id, 'refund'), {
        amount,
        reason
      });
      return response;
    } catch (error) {
      console.error('Payment refund failed:', error);
      throw new Error(
        'Erstattung erfordert Serververbindung (RKSV). Bitte online erneut versuchen.'
      );
    }
  }

  /**
   * @deprecated Backend has no daily-report route. Use Tagesabschluss/statistics or
   * date-range payments instead. This method throws; do not use in new code.
   */
  async getDailyPaymentReport(_date: string): Promise<{
    totalPayments: number;
    totalAmount: number;
    paymentMethodBreakdown: Record<string, number>;
    payments: PaymentResponse[];
  }> {
    throw new Error(
      'getDailyPaymentReport is unsupported: backend does not expose daily-report. Use Tagesabschluss statistics or payment date-range APIs.'
    );
  }

  // Ödeme istatistikleri
  async getPaymentStatistics(period: 'day' | 'week' | 'month' | 'year'): Promise<{
    totalPayments: number;
    totalAmount: number;
    averageAmount: number;
    topPaymentMethods: { method: string; count: number; amount: number }[];
  }> {
    try {
      const raw = await apiClient.get<unknown>(
        `${this.baseUrl}/statistics?period=${encodeURIComponent(period)}`
      );
      const layer = unwrapApiResponseLayer(raw);
      const payload =
        isRecord(layer) && layer.data != null && typeof layer.data === 'object'
          ? layer.data
          : layer;
      return payload as {
        totalPayments: number;
        totalAmount: number;
        averageAmount: number;
        topPaymentMethods: { method: string; count: number; amount: number }[];
      };
    } catch (error) {
      console.error('Payment statistics failed:', error);
      throw new Error(
        'Statistik nicht verfügbar (keine Serververbindung oder Fehler).'
      );
    }
  }

  /**
   * Retry pending NON_FISCAL rows against POST /api/pos/payment.
   * Returns count of successfully synced entries.
   */
  async syncOfflinePayments(): Promise<number> {
    try {
      const { processed } = await flushPendingPaymentQueue();
      return processed;
    } catch (error) {
      console.error('Payment sync failed:', error);
      return 0;
    }
  }

  /** Expose full sync result for UI/logging. */
  async syncPendingPaymentQueue(): Promise<{ processed: number; failed: number }> {
    return flushPendingPaymentQueue();
  }

  /** NON_FISCAL rows waiting for server POST /api/pos/payment (isSynced false). */
  async listPendingNonFiscalPayments() {
    return getPendingPaymentQueue();
  }
}

export const paymentService = new PaymentService();
export default paymentService; 