import i18n from '../../i18n';
import { mapBackendCashRegisterCodeToGerman } from '../../utils/posRegisterGateCopy';

export type PaymentErrorCode =
  | 'DEMO_PAYMENT_RESTRICTED'
  | 'BENEFIT_DAILY_ALLOWANCE_CONFLICT'
  | 'TSE_UNAVAILABLE'
  | 'LICENSE_LOCKED'
  | 'MONATSBELEG_REQUIRED'
  | 'UNKNOWN_PAYMENT_ERROR';

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

const TSE_UNAVAILABLE_CODES = new Set(['TSE_UNAVAILABLE', 'TSE_NOT_CONNECTED']);
const LICENSE_LOCKED_CODES = new Set(['LICENSE_LOCKED', 'DEPLOYMENT_LICENSE_LOCKDOWN']);
const MONATSBELEG_CODES = new Set(['CASH_REGISTER_MONATSBELEG_REQUIRED', 'MONATSBELEG_REQUIRED']);

export function isTseUnavailableDiagnostic(code?: string | null): boolean {
  return typeof code === 'string' && TSE_UNAVAILABLE_CODES.has(code.trim().toUpperCase());
}

export function isLicenseLockedDiagnostic(code?: string | null): boolean {
  return typeof code === 'string' && LICENSE_LOCKED_CODES.has(code.trim().toUpperCase());
}

export function isMonatsbelegRequiredDiagnostic(code?: string | null): boolean {
  return typeof code === 'string' && MONATSBELEG_CODES.has(code.trim().toUpperCase());
}

function getData(obj: unknown): Record<string, unknown> | undefined {
  if (!obj || typeof obj !== 'object') return undefined;
  const e = obj as Record<string, unknown>;
  const direct = e['data'];
  if (direct && typeof direct === 'object') return direct as Record<string, unknown>;

  const response = e['response'];
  if (response && typeof response === 'object') {
    const r = response as Record<string, unknown>;
    const responseData = r['data'];
    if (responseData && typeof responseData === 'object') {
      return responseData as Record<string, unknown>;
    }
  }

  return undefined;
}

function getStatus(obj: unknown): number | undefined {
  if (!obj || typeof obj !== 'object') return undefined;
  const e = obj as Record<string, unknown>;
  const status = e.status ?? (e.response as Record<string, unknown>)?.status;
  return typeof status === 'number' ? status : undefined;
}

function resolveDiagnosticCode(
  data: Record<string, unknown> | undefined,
  details: Record<string, unknown> | undefined
): string | undefined {
  return (
    (typeof details?.diagnosticCode === 'string' ? details.diagnosticCode : undefined) ??
    (typeof details?.code === 'string' ? details.code : undefined) ??
    (typeof data?.code === 'string' ? data.code : undefined) ??
    (typeof data?.diagnosticCode === 'string' ? data.diagnosticCode : undefined)
  );
}

export function normalizePaymentError(error: unknown): PaymentAppError {
  if (isPaymentError(error)) return error;

  const status = getStatus(error);
  const data = getData(error);
  const details = data?.details as Record<string, unknown> | undefined;
  const diagnosticCode = resolveDiagnosticCode(data, details);

  if (status === 400 && diagnosticCode === DEMO_BY_ROLE) {
    return new PaymentAppError('DEMO_PAYMENT_RESTRICTED', 400, DEMO_BY_ROLE);
  }

  const code = (data?.code ?? details?.code) as string | undefined;
  if (status === 409 && code === 'BENEFIT_DAILY_ALLOWANCE_CONFLICT') {
    return new PaymentAppError('BENEFIT_DAILY_ALLOWANCE_CONFLICT', 409, code);
  }

  if (isTseUnavailableDiagnostic(diagnosticCode) || isTseUnavailableDiagnostic(code)) {
    return new PaymentAppError('TSE_UNAVAILABLE', status, diagnosticCode ?? code);
  }

  if (isLicenseLockedDiagnostic(diagnosticCode) || isLicenseLockedDiagnostic(code) || status === 403) {
    if (isLicenseLockedDiagnostic(diagnosticCode) || isLicenseLockedDiagnostic(code)) {
      return new PaymentAppError('LICENSE_LOCKED', status, diagnosticCode ?? code);
    }
  }

  if (isMonatsbelegRequiredDiagnostic(diagnosticCode) || isMonatsbelegRequiredDiagnostic(code)) {
    return new PaymentAppError('MONATSBELEG_REQUIRED', status, diagnosticCode ?? code);
  }

  if (diagnosticCode?.startsWith('CASH_REGISTER_')) {
    return new PaymentAppError('UNKNOWN_PAYMENT_ERROR', status, diagnosticCode);
  }

  return new PaymentAppError('UNKNOWN_PAYMENT_ERROR', status, diagnosticCode);
}

const PAYMENT_ERROR_I18N_KEYS: Record<PaymentErrorCode, string> = {
  DEMO_PAYMENT_RESTRICTED: 'payment:errors.demoRestricted',
  BENEFIT_DAILY_ALLOWANCE_CONFLICT: 'payment:errors.benefitDailyAllowanceConflict',
  TSE_UNAVAILABLE: 'payment:errors.tseUnavailable',
  LICENSE_LOCKED: 'payment:errors.licenseLocked',
  MONATSBELEG_REQUIRED: 'payment:errors.monatsbelegRequired',
  UNKNOWN_PAYMENT_ERROR: 'payment:errors.retry',
};

export function getPaymentErrorMessage(code: PaymentErrorCode): string {
  return i18n.t(PAYMENT_ERROR_I18N_KEYS[code]);
}

/** User-facing message: cash-register diagnostics (German) override i18n for UNKNOWN. */
export function getPaymentErrorDisplayMessage(err: unknown): string {
  if (isPaymentError(err)) {
    if (err.code === 'TSE_UNAVAILABLE' || err.code === 'LICENSE_LOCKED' || err.code === 'MONATSBELEG_REQUIRED') {
      return getPaymentErrorMessage(err.code);
    }
    const reg = mapBackendCashRegisterCodeToGerman(err.diagnosticCode);
    if (reg) return reg;
    return getPaymentErrorMessage(err.code);
  }
  return err instanceof Error ? err.message : i18n.t('payment:errors.retry');
}

function looksLikeTseFailure(message?: string, error?: string): boolean {
  const hint = `${message || ''} ${error || ''}`.toLowerCase();
  return hint.includes('tse') || hint.includes('signatur') || hint.includes('signature');
}

/** Maps a failed payment API body (success:false) to German operator copy when possible. */
export function getPaymentResponseFailureMessage(response: {
  message?: string;
  error?: string;
  diagnosticCode?: string;
  fiscalStatus?: string;
}): string {
  if (isTseUnavailableDiagnostic(response.diagnosticCode)) {
    return i18n.t('payment:errors.tseUnavailable');
  }
  if (isLicenseLockedDiagnostic(response.diagnosticCode)) {
    return i18n.t('payment:errors.licenseLocked');
  }
  if (isMonatsbelegRequiredDiagnostic(response.diagnosticCode)) {
    return i18n.t('payment:errors.monatsbelegRequired');
  }
  const reg = mapBackendCashRegisterCodeToGerman(response.diagnosticCode);
  if (reg) return reg;
  if (!response.diagnosticCode && looksLikeTseFailure(response.message, response.error)) {
    return i18n.t('payment:errors.tseUnavailable');
  }
  if (response.fiscalStatus === 'FAILED') {
    return response.message || response.error || getPaymentErrorMessage('UNKNOWN_PAYMENT_ERROR');
  }
  return response.message || response.error || getPaymentErrorMessage('UNKNOWN_PAYMENT_ERROR');
}
