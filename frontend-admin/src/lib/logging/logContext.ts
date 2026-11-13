/**
 * Ambient log context (component, userId, sessionId) merged into every structured record.
 * Safe values only — never store tokens or passwords here.
 */

export type LogContext = {
  component?: string;
  userId?: string | null;
  sessionId?: string | null;
  tenantId?: string | null;
  route?: string | null;
};

const SESSION_STORAGE_KEY = 'rk_admin_log_session_id';

let ambient: LogContext = {};

function createSessionId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `sess_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 10)}`;
}

/** Stable per-tab session id (not an auth token). Browser-only. */
export function getOrCreateClientSessionId(): string | null {
  if (typeof window === 'undefined') {
    return null;
  }
  try {
    const existing = window.sessionStorage.getItem(SESSION_STORAGE_KEY);
    if (existing && existing.length > 0 && existing.length <= 80) {
      return existing;
    }
    const next = createSessionId();
    window.sessionStorage.setItem(SESSION_STORAGE_KEY, next);
    return next;
  } catch {
    return createSessionId();
  }
}

export function getLogContext(): LogContext {
  return { ...ambient };
}

/** Merge into ambient context (partial update). Pass `null` fields to clear. */
export function bindLogContext(partial: LogContext): void {
  ambient = {
    ...ambient,
    ...partial,
  };
}

export function clearLogContext(): void {
  ambient = {};
}

/** Return a copy with null/undefined/empty strings omitted. */
export function compactLogContext(ctx: LogContext): Record<string, string> {
  const out: Record<string, string> = {};
  for (const [key, value] of Object.entries(ctx)) {
    if (typeof value === 'string' && value.trim().length > 0) {
      out[key] = value.trim().slice(0, 128);
    }
  }
  return out;
}
