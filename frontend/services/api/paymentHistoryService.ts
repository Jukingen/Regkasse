import { apiClient } from './config';
import { isRecord, unwrapApiResponseLayer } from './normalizePosPaymentMethods';
import { POS_PAYMENT_API_PREFIX } from './posPaymentPaths';

export const POS_PAYMENT_HISTORY_PATH = `${POS_PAYMENT_API_PREFIX}/history` as const;
export const POS_STORNO_PATH = '/pos/storno' as const;

export type PaymentHistoryReasonOption = {
  code: string;
  labelKey: string;
};

export type PaymentHistoryAvailableAction = {
  action: string;
  labelKey: string;
  requiresReason: boolean;
  requiresManagerApproval: boolean;
  reasonLabelKey?: string | null;
  reasonOptions: PaymentHistoryReasonOption[];
};

export type PaymentHistoryItem = {
  id: string;
  receiptNumber: string;
  totalAmount: number;
  createdAt: string;
  paymentMethod: string;
  customerName: string;
  tableNumber?: number | null;
  isStorno: boolean;
  isRefund: boolean;
  availableActions: PaymentHistoryAvailableAction[];
};

export type PaymentHistoryResponse = {
  payments: PaymentHistoryItem[];
  totalCount: number;
  limit: number;
  offset: number;
  hasMore: boolean;
  fromUtc: string;
  toUtc: string;
  cashRegisterId: string;
  language: string;
};

export type StornoRequestPayload = {
  paymentId: string;
  reasonCode: string;
  reason: string;
  approvalToken?: string;
  idempotencyKey?: string;
};

export type StornoResponsePayload = {
  success: boolean;
  errorKey?: string | null;
  messageKey?: string | null;
  stornoPaymentId?: string | null;
  requiresApproval?: boolean;
  approvalRequestId?: string | null;
  approvalTokenExpiresAtUtc?: string | null;
  diagnosticCode?: string | null;
};

export type FetchPaymentHistoryParams = {
  hours?: number;
  language?: string;
  limit?: number;
  offset?: number;
  cashRegisterId?: string | null;
};

function readString(raw: unknown, fallback = ''): string {
  return typeof raw === 'string' ? raw : fallback;
}

function readNumber(raw: unknown, fallback = 0): number {
  if (typeof raw === 'number' && Number.isFinite(raw)) return raw;
  if (typeof raw === 'string' && raw.trim() !== '') {
    const n = Number(raw.replace(',', '.'));
    if (Number.isFinite(n)) return n;
  }
  return fallback;
}

function readBool(raw: unknown, fallback = false): boolean {
  return typeof raw === 'boolean' ? raw : fallback;
}

function parseReasonOption(raw: unknown): PaymentHistoryReasonOption | null {
  if (!isRecord(raw)) return null;
  const code = readString(raw.code ?? raw.Code);
  const labelKey = readString(raw.labelKey ?? raw.LabelKey);
  if (!code || !labelKey) return null;
  return { code, labelKey };
}

function parseAvailableAction(raw: unknown): PaymentHistoryAvailableAction | null {
  if (!isRecord(raw)) return null;
  const action = readString(raw.action ?? raw.Action);
  const labelKey = readString(raw.labelKey ?? raw.LabelKey);
  if (!action || !labelKey) return null;
  const reasonOptionsRaw = raw.reasonOptions ?? raw.ReasonOptions;
  const reasonOptions = Array.isArray(reasonOptionsRaw)
    ? reasonOptionsRaw
        .map(parseReasonOption)
        .filter((x): x is PaymentHistoryReasonOption => x != null)
    : [];
  return {
    action,
    labelKey,
    requiresReason: readBool(raw.requiresReason ?? raw.RequiresReason),
    requiresManagerApproval: readBool(raw.requiresManagerApproval ?? raw.RequiresManagerApproval),
    reasonLabelKey: readString(raw.reasonLabelKey ?? raw.ReasonLabelKey) || null,
    reasonOptions,
  };
}

export function parsePaymentHistoryItem(raw: unknown): PaymentHistoryItem | null {
  if (!isRecord(raw)) return null;
  const id = readString(raw.id ?? raw.Id);
  if (!id) return null;
  const actionsRaw = raw.availableActions ?? raw.AvailableActions;
  const availableActions = Array.isArray(actionsRaw)
    ? actionsRaw
        .map(parseAvailableAction)
        .filter((x): x is PaymentHistoryAvailableAction => x != null)
    : [];
  const tableRaw = raw.tableNumber ?? raw.TableNumber;
  return {
    id,
    receiptNumber: readString(raw.receiptNumber ?? raw.ReceiptNumber),
    totalAmount: readNumber(raw.totalAmount ?? raw.TotalAmount),
    createdAt: readString(raw.createdAt ?? raw.CreatedAt),
    paymentMethod: readString(raw.paymentMethod ?? raw.PaymentMethod),
    customerName: readString(raw.customerName ?? raw.CustomerName, 'Walk-in'),
    tableNumber: tableRaw == null || tableRaw === '' ? null : readNumber(tableRaw, 0) || null,
    isStorno: readBool(raw.isStorno ?? raw.IsStorno),
    isRefund: readBool(raw.isRefund ?? raw.IsRefund),
    availableActions,
  };
}

export function parsePaymentHistoryResponse(raw: unknown): PaymentHistoryResponse {
  const layer = unwrapApiResponseLayer(raw);
  if (!isRecord(layer)) {
    return {
      payments: [],
      totalCount: 0,
      limit: 20,
      offset: 0,
      hasMore: false,
      fromUtc: '',
      toUtc: '',
      cashRegisterId: '',
      language: 'de',
    };
  }
  const paymentsRaw = layer.payments ?? layer.Payments ?? layer.items ?? layer.Items;
  const payments = Array.isArray(paymentsRaw)
    ? paymentsRaw.map(parsePaymentHistoryItem).filter((x): x is PaymentHistoryItem => x != null)
    : [];
  return {
    payments,
    totalCount: readNumber(layer.totalCount ?? layer.TotalCount, payments.length),
    limit: readNumber(layer.limit ?? layer.Limit, 20),
    offset: readNumber(layer.offset ?? layer.Offset),
    hasMore: readBool(layer.hasMore ?? layer.HasMore),
    fromUtc: readString(layer.fromUtc ?? layer.FromUtc),
    toUtc: readString(layer.toUtc ?? layer.ToUtc),
    cashRegisterId: readString(layer.cashRegisterId ?? layer.CashRegisterId),
    language: readString(layer.language ?? layer.Language, 'de'),
  };
}

export function parseStornoResponse(raw: unknown): StornoResponsePayload {
  const layer = unwrapApiResponseLayer(raw);
  if (!isRecord(layer)) {
    return { success: false, errorKey: 'errors.stornoFailed' };
  }
  const approvalId = readString(layer.approvalRequestId ?? layer.ApprovalRequestId);
  return {
    success: readBool(layer.success ?? layer.Success),
    errorKey: readString(layer.errorKey ?? layer.ErrorKey) || null,
    messageKey: readString(layer.messageKey ?? layer.MessageKey) || null,
    stornoPaymentId: readString(layer.stornoPaymentId ?? layer.StornoPaymentId) || null,
    requiresApproval: readBool(layer.requiresApproval ?? layer.RequiresApproval),
    approvalRequestId: approvalId || null,
    approvalTokenExpiresAtUtc:
      readString(layer.approvalTokenExpiresAtUtc ?? layer.ApprovalTokenExpiresAtUtc) || null,
    diagnosticCode: readString(layer.diagnosticCode ?? layer.DiagnosticCode) || null,
  };
}

export async function fetchPaymentHistory(
  params: FetchPaymentHistoryParams = {}
): Promise<PaymentHistoryResponse> {
  const response = await apiClient.get<unknown>(POS_PAYMENT_HISTORY_PATH, {
    params: {
      hours: params.hours ?? 24,
      language: params.language ?? 'de',
      limit: params.limit ?? 20,
      offset: params.offset ?? 0,
      ...(params.cashRegisterId ? { cashRegisterId: params.cashRegisterId } : {}),
    },
  });
  return parsePaymentHistoryResponse(response);
}

export async function postStorno(payload: StornoRequestPayload): Promise<StornoResponsePayload> {
  const response = await apiClient.post<unknown>(POS_STORNO_PATH, {
    paymentId: payload.paymentId,
    reasonCode: payload.reasonCode,
    reason: payload.reason,
    approvalToken: payload.approvalToken,
    idempotencyKey: payload.idempotencyKey,
  });
  return parseStornoResponse(response);
}

/** Maps backend dotted keys to i18next `paymentHistory:…` paths. */
export function paymentHistoryLabelKeyToI18n(labelKey: string): string {
  const trimmed = labelKey.trim();
  if (!trimmed) return trimmed;
  if (trimmed.includes(':')) return trimmed;
  if (trimmed.startsWith('paymentHistory.')) {
    return `paymentHistory:${trimmed.slice('paymentHistory.'.length)}`;
  }
  if (trimmed.startsWith('errors.') || trimmed.startsWith('messages.')) {
    return `paymentHistory:${trimmed}`;
  }
  const dot = trimmed.indexOf('.');
  if (dot <= 0) return trimmed;
  return `${trimmed.slice(0, dot)}:${trimmed.slice(dot + 1)}`;
}
