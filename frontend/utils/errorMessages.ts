/**
 * User-facing Tagesabschluss (daily closing) error messages for POS.
 * Maps machine codes / backend English messages → clear German next steps.
 *
 * Codes align with POS `/api/pos/shift/daily-closing` behaviour
 * (`PosDailyClosingBlockReasons`, `PosDailyClosingFailureKind`, and fiscal messages).
 */

export type DailyClosingErrorCode =
  | 'NO_ACTIVE_SHIFT'
  | 'ALREADY_CLOSED'
  | 'PAYMENTS_WITHOUT_INVOICE'
  | 'REGISTER_UNAVAILABLE'
  | 'REGISTER_CLOSE_FAILED'
  | 'TSE_UNAVAILABLE'
  | 'NO_SALES_TODAY'
  | 'PERMISSION_DENIED'
  | 'NETWORK_ERROR'
  | 'TIMEOUT'
  | 'BACKEND_ERROR'
  | 'UNKNOWN'
  // Forward-compatible aliases (not currently emitted by POS shift API)
  | 'SHIFT_NOT_CLOSED'
  | 'PENDING_ORDERS'
  | 'OFFLINE_TRANSACTIONS';

export const dailyClosingErrorMessages: Record<DailyClosingErrorCode, string> = {
  NO_ACTIVE_SHIFT:
    'Es ist keine Schicht geöffnet. Bitte starten Sie zuerst eine Schicht und versuchen Sie es erneut.',
  ALREADY_CLOSED:
    'Der Tagesabschluss für heute wurde bereits durchgeführt. Sie können die Schicht noch beenden.',
  PAYMENTS_WITHOUT_INVOICE:
    'Es gibt Zahlungen ohne passende Rechnung. Bitte beheben Sie diese zuerst und versuchen Sie den Tagesabschluss erneut.',
  REGISTER_UNAVAILABLE:
    'Diese Kasse ist für den Tagesabschluss nicht verfügbar. Bitte wählen Sie eine aktive Kasse oder kontaktieren Sie den Administrator.',
  REGISTER_CLOSE_FAILED:
    'Der Tagesabschluss wurde erstellt, aber die Kasse konnte nicht geschlossen werden. Bitte kontaktieren Sie den Administrator.',
  TSE_UNAVAILABLE:
    'Die Signiereinheit (TSE) ist nicht erreichbar. Bitte prüfen Sie die Verbindung und versuchen Sie es erneut.',
  NO_SALES_TODAY: 'Für heute gibt es keine Umsätze. Ein Tagesabschluss ist derzeit nicht möglich.',
  PERMISSION_DENIED:
    'Sie haben keine Berechtigung, einen Tagesabschluss durchzuführen. Bitte wenden Sie sich an Ihren Vorgesetzten.',
  NETWORK_ERROR:
    'Keine Verbindung zum Server. Bitte prüfen Sie Ihre Internetverbindung und versuchen Sie es erneut.',
  TIMEOUT: 'Die Anfrage hat zu lange gedauert. Bitte versuchen Sie es in wenigen Momenten erneut.',
  BACKEND_ERROR:
    'Ein technischer Fehler ist aufgetreten. Bitte kontaktieren Sie den Administrator.',
  UNKNOWN: 'Ein unbekannter Fehler ist aufgetreten. Bitte kontaktieren Sie den Administrator.',
  // Product note: POS Tagesabschluss requires an open shift (not "close shift first").
  SHIFT_NOT_CLOSED:
    'Es ist keine Schicht geöffnet. Bitte starten Sie zuerst eine Schicht und versuchen Sie es erneut.',
  PENDING_ORDERS:
    'Es gibt noch offene Bestellungen. Bitte schließen oder stornieren Sie diese zuerst.',
  OFFLINE_TRANSACTIONS:
    'Es gibt noch nicht synchronisierte Offline-Transaktionen. Bitte synchronisieren Sie zuerst.',
};

const BLOCK_REASON_TO_CODE: Record<string, DailyClosingErrorCode> = {
  already_closed_today: 'ALREADY_CLOSED',
  payments_without_invoice: 'PAYMENTS_WITHOUT_INVOICE',
  register_unavailable: 'REGISTER_UNAVAILABLE',
  no_active_shift: 'NO_ACTIVE_SHIFT',
};

export type DailyClosingErrorClassifyInput = {
  code?: string | null;
  blockReason?: string | null;
  message?: string | null;
  httpStatus?: number | null;
  axiosCode?: string | null;
  paymentsWithoutInvoiceCount?: number | null;
};

function normalizeCode(raw: string): DailyClosingErrorCode | null {
  const key = raw.trim().toUpperCase().replace(/-/g, '_');
  if (key in dailyClosingErrorMessages) {
    return key as DailyClosingErrorCode;
  }
  return null;
}

function classifyFromMessage(message: string): DailyClosingErrorCode | null {
  const m = message.toLowerCase();

  if (m.includes('no active shift')) return 'NO_ACTIVE_SHIFT';
  if (m.includes('already performed') || m.includes('already closed')) return 'ALREADY_CLOSED';
  if (/without (a matching )?invoice/.test(m) || m.includes('payments_without_invoice')) {
    return 'PAYMENTS_WITHOUT_INVOICE';
  }
  if (/not available for daily closing|cash register .* not found|decommissioned/.test(m)) {
    return 'REGISTER_UNAVAILABLE';
  }
  if (m.includes('not allowed to close this cash register')) return 'PERMISSION_DENIED';
  if (m.includes('could not be closed after daily closing')) return 'REGISTER_CLOSE_FAILED';
  if (/tse device is not connected|tse.*(unavailable|not connected|offline)/.test(m)) {
    return 'TSE_UNAVAILABLE';
  }
  if (m.includes('no transactions found')) return 'NO_SALES_TODAY';
  if (/network error|failed to fetch|internet/.test(m)) return 'NETWORK_ERROR';
  if (/timeout|timed out|aborted/.test(m)) return 'TIMEOUT';

  return null;
}

/**
 * Resolve a stable error code from API / transport failure details.
 */
export function classifyDailyClosingError(
  input: DailyClosingErrorClassifyInput
): DailyClosingErrorCode {
  if (
    typeof input.paymentsWithoutInvoiceCount === 'number' &&
    input.paymentsWithoutInvoiceCount > 0
  ) {
    return 'PAYMENTS_WITHOUT_INVOICE';
  }

  if (input.code) {
    const fromCode = normalizeCode(input.code);
    if (fromCode) return fromCode;
  }

  if (input.blockReason) {
    const mapped = BLOCK_REASON_TO_CODE[input.blockReason.trim().toLowerCase()];
    if (mapped) return mapped;
  }

  const axiosCode = input.axiosCode?.toUpperCase() ?? '';
  if (axiosCode === 'ECONNABORTED') return 'TIMEOUT';
  if (axiosCode === 'ERR_NETWORK') return 'NETWORK_ERROR';

  const status = input.httpStatus ?? undefined;
  if (status === 403) return 'PERMISSION_DENIED';
  if (status === 401) return 'PERMISSION_DENIED';
  if (status == null && !input.message) return 'NETWORK_ERROR';

  if (input.message) {
    const fromMessage = classifyFromMessage(input.message);
    if (fromMessage) return fromMessage;
  }

  if (status != null && status >= 500) return 'BACKEND_ERROR';
  if (status === 404) return 'NO_ACTIVE_SHIFT';

  return 'UNKNOWN';
}

export type GetDailyClosingErrorMessageOptions = {
  /** When set, appends the open payment count for PAYMENTS_WITHOUT_INVOICE. */
  count?: number;
};

export function getDailyClosingErrorMessage(
  errorCode: string,
  options?: GetDailyClosingErrorMessageOptions
): string {
  const code = normalizeCode(errorCode) ?? 'UNKNOWN';
  let message = dailyClosingErrorMessages[code] ?? dailyClosingErrorMessages.UNKNOWN;

  if (
    code === 'PAYMENTS_WITHOUT_INVOICE' &&
    typeof options?.count === 'number' &&
    options.count > 0
  ) {
    message = `${options.count} Zahlung(en) ohne passende Rechnung. Bitte beheben Sie diese zuerst und versuchen Sie den Tagesabschluss erneut.`;
  }

  return message;
}

/**
 * Classify an unknown thrown value and return a cashier-facing German message.
 */
export function resolveDailyClosingFailureMessage(error: unknown): string {
  const classified = extractDailyClosingErrorDetails(error);
  return getDailyClosingErrorMessage(classified.code, { count: classified.count });
}

export type ExtractedDailyClosingError = {
  code: DailyClosingErrorCode;
  count?: number;
  technicalMessage?: string;
};

export function extractDailyClosingErrorDetails(error: unknown): ExtractedDailyClosingError {
  const e = error as {
    code?: string;
    message?: string;
    paymentsWithoutInvoiceCount?: number;
    httpStatus?: number;
    status?: number;
    response?: { status?: number; data?: unknown };
    data?: unknown;
  } | null;

  const data = (e?.response?.data ?? e?.data) as Record<string, unknown> | undefined;
  const technicalMessage =
    (typeof data?.error === 'string' && data.error) ||
    (typeof data?.message === 'string' && data.message) ||
    (typeof e?.message === 'string' && e.message) ||
    undefined;

  const countFromData =
    typeof data?.paymentsWithoutInvoiceCount === 'number'
      ? data.paymentsWithoutInvoiceCount
      : undefined;
  const count =
    typeof e?.paymentsWithoutInvoiceCount === 'number'
      ? e.paymentsWithoutInvoiceCount
      : countFromData;

  const codeFromData =
    (typeof data?.code === 'string' && data.code) ||
    (typeof data?.errorCode === 'string' && data.errorCode) ||
    (typeof data?.blockReason === 'string' && data.blockReason) ||
    undefined;

  const httpStatus = e?.httpStatus ?? e?.response?.status ?? e?.status ?? null;
  const rawCode = typeof e?.code === 'string' ? e.code : null;
  const axiosCode =
    rawCode && (/^ERR_/i.test(rawCode) || rawCode === 'ECONNABORTED') ? rawCode : null;

  const code = classifyDailyClosingError({
    code: rawCode && !axiosCode ? rawCode : codeFromData,
    message: technicalMessage,
    httpStatus,
    axiosCode,
    paymentsWithoutInvoiceCount: count,
  });

  return {
    code,
    count: typeof count === 'number' ? count : undefined,
    technicalMessage,
  };
}
