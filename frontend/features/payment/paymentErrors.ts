import i18n from '../../i18n';

export type PaymentErrorCode = 'DEMO_PAYMENT_RESTRICTED' | 'UNKNOWN_PAYMENT_ERROR';

export class PaymentAppError extends Error {
  readonly code: PaymentErrorCode;
  readonly status?: number;
  readonly diagnosticCode?: string;

  constructor(code: PaymentErrorCode, status?: number, diagnosticCode?: string) {
    super(code);
    this.code = code;
    this.status = status;
    this.diagnosticCode = diagnosticCode;
    this.name = 'PaymentAppError';
  }
}

export function isPaymentError(error: unknown): error is PaymentAppError {
  return error instanceof PaymentAppError;
}

const DEMO_BY_ROLE = 'DEMO_BY_ROLE';

function getData(obj: unknown): Record<string, unknown> | undefined {
  if (!obj || typeof obj !== 'object') return undefined;
  const e = obj as Record<string, unknown>;
  return (e.data ?? e.response?.data) as Record<string, unknown> | undefined;
}

function getStatus(obj: unknown): number | undefined {
  if (!obj || typeof obj !== 'object') return undefined;
  const e = obj as Record<string, unknown>;
  const status = e.status ?? (e.response as Record<string, unknown>)?.status;
  return typeof status === 'number' ? status : undefined;
}

export function normalizePaymentError(error: unknown): PaymentAppError {
  if (isPaymentError(error)) return error;

  const status = getStatus(error);
  const data = getData(error);
  const details = data?.details as { diagnosticCode?: string } | undefined;
  const diagnosticCode = details?.diagnosticCode;

  if (status === 400 && diagnosticCode === DEMO_BY_ROLE) {
    return new PaymentAppError('DEMO_PAYMENT_RESTRICTED', 400, DEMO_BY_ROLE);
  }

  return new PaymentAppError('UNKNOWN_PAYMENT_ERROR', status);
}

const PAYMENT_ERROR_I18N_KEYS: Record<PaymentErrorCode, string> = {
  DEMO_PAYMENT_RESTRICTED: 'payment:errors.demoRestricted',
  UNKNOWN_PAYMENT_ERROR: 'payment:errors.unknown',
};

export function getPaymentErrorMessage(code: PaymentErrorCode): string {
  return i18n.t(PAYMENT_ERROR_I18N_KEYS[code]);
}
