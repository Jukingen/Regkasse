/**
 * Normalized frontend error model extracted from transport/unknown failures in one pass.
 * Shape stays stable when the backend contract evolves; code-based translation uses `code` + registry.
 */

export type ApiErrorSeverity = 'info' | 'warning' | 'error';

export type NormalizedApiError = {
  /** HTTP response status (axios response.status) */
  httpStatus: number | undefined;
  /**
   * Machine-oriented error code (future contract; today heuristically read from common response.data fields).
   */
  code: string | undefined;
  /** Single-line server/error text (for copyable raw block) */
  rawMessage: string | undefined;
  /** ModelState / validation: field → message list */
  fieldErrors: Record<string, string[]> | undefined;
  severity: ApiErrorSeverity | undefined;
  retryable: boolean | undefined;
  remediationHint: string | undefined;
  /** Optional correlation id (logging) */
  traceId: string | undefined;
};

function isRecord(v: unknown): v is Record<string, unknown> {
  return v != null && typeof v === 'object' && !Array.isArray(v);
}

function firstValidationMessage(errors: unknown): string | undefined {
  if (!isRecord(errors)) return undefined;
  for (const key of Object.keys(errors)) {
    const val = errors[key];
    if (Array.isArray(val) && val.length > 0 && typeof val[0] === 'string') return val[0].trim();
    if (typeof val === 'string') return val.trim();
  }
  return undefined;
}

export function collectFieldErrors(errors: unknown): Record<string, string[]> | undefined {
  if (!isRecord(errors)) return undefined;
  const out: Record<string, string[]> = {};
  for (const key of Object.keys(errors)) {
    const val = errors[key];
    if (Array.isArray(val)) {
      const msgs = val.filter((x): x is string => typeof x === 'string').map((s) => s.trim()).filter(Boolean);
      if (msgs.length) out[key] = msgs;
    } else if (typeof val === 'string' && val.trim()) {
      out[key] = [val.trim()];
    }
  }
  return Object.keys(out).length ? out : undefined;
}

/** Do not use RFC7807 `type` URLs or long plain text as the machine code. */
function pickBackendCode(data: Record<string, unknown> | undefined): string | undefined {
  if (!data) return undefined;
  const keys = ['code', 'errorCode', 'error_code'] as const;
  for (const k of keys) {
    const v = data[k];
    if (typeof v === 'string' && v.trim()) {
      const s = v.trim();
      if (s.length > 96) continue;
      if (/^https?:\/\//i.test(s)) continue;
      return s;
    }
  }
  const t = data.type;
  if (typeof t === 'string' && t.trim()) {
    const s = t.trim();
    if (!/^https?:\/\//i.test(s) && s.length <= 96) return s;
  }
  return undefined;
}

function pickSeverity(data: Record<string, unknown> | undefined): ApiErrorSeverity | undefined {
  if (!data) return undefined;
  const s = data.severity;
  if (s === 'info' || s === 'warning' || s === 'error') return s;
  return undefined;
}

function pickRetryable(data: Record<string, unknown> | undefined): boolean | undefined {
  if (!data) return undefined;
  const keys = ['retryable', 'isRetryable', 'retry'] as const;
  for (const k of keys) {
    const v = data[k];
    if (typeof v === 'boolean') return v;
  }
  return undefined;
}

function pickRemediation(data: Record<string, unknown> | undefined): string | undefined {
  if (!data) return undefined;
  const keys = ['remediationHint', 'remediation', 'hint', 'userHint'] as const;
  for (const k of keys) {
    const v = data[k];
    if (typeof v === 'string' && v.trim()) return v.trim();
  }
  return undefined;
}

function pickTraceId(data: Record<string, unknown> | undefined): string | undefined {
  if (!data) return undefined;
  const keys = ['traceId', 'trace_id', 'requestId', 'request_id'] as const;
  for (const k of keys) {
    const v = data[k];
    if (typeof v === 'string' && v.trim()) return v.trim();
  }
  return undefined;
}

/**
 * Compatible with Axios/orval/ProblemDetails and client wrappers exposing `normalized.message`.
 * Single parse entry: `extractRawApiErrorMessage` / UI consumes this shape.
 */
export function normalizeApiError(error: unknown): NormalizedApiError {
  if (error == null) {
    return {
      httpStatus: undefined,
      code: undefined,
      rawMessage: undefined,
      fieldErrors: undefined,
      severity: undefined,
      retryable: undefined,
      remediationHint: undefined,
      traceId: undefined,
    };
  }

  const e = error as {
    response?: { status?: number; data?: unknown };
    message?: string;
    normalized?: { message?: string };
  };

  const httpStatus =
    typeof e.response?.status === 'number' && Number.isFinite(e.response.status)
      ? e.response.status
      : undefined;

  const data = e.response?.data;
  const d = isRecord(data) ? data : undefined;

  const normalizedMsg = e.normalized?.message;
  const normalizedTrimmed =
    typeof normalizedMsg === 'string' && normalizedMsg.trim() ? normalizedMsg.trim() : undefined;

  const fieldErrors = d && d.errors != null ? collectFieldErrors(d.errors) : undefined;
  const validationFirst = d?.errors != null ? firstValidationMessage(d.errors) : undefined;

  const rawMessage =
    (typeof d?.message === 'string' && d.message.trim()) ||
    validationFirst ||
    (typeof d?.title === 'string' && d.title.trim()) ||
    (typeof d?.error === 'string' && d.error.trim()) ||
    (typeof d?.detail === 'string' && d.detail.trim()) ||
    (typeof d?.details === 'string' && d.details.trim()) ||
    (typeof d?.reason === 'string' && d.reason.trim()) ||
    normalizedTrimmed ||
    (typeof e.message === 'string' && e.message.trim()) ||
    undefined;

  return {
    httpStatus,
    code: pickBackendCode(d),
    rawMessage: rawMessage || undefined,
    fieldErrors,
    severity: pickSeverity(d),
    retryable: pickRetryable(d),
    remediationHint: pickRemediation(d),
    traceId: pickTraceId(d),
  };
}
