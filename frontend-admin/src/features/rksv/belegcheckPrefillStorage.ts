/**
 * One-shot handoff from receipt detail → /rksv/belegcheck (avoids huge URL query strings).
 * Session-only; cleared after read on the target page.
 */
export const RKSV_BELEGCHECK_PREFILL_SESSION_KEY = 'rksv.belegcheck.prefill.v1';

export function setBelegcheckPrefillSession(qrPayload: string): void {
  if (typeof window === 'undefined') return;
  try {
    sessionStorage.setItem(RKSV_BELEGCHECK_PREFILL_SESSION_KEY, qrPayload);
  } catch {
    // ignore quota / private mode
  }
}

/** Returns stored payload and removes the key so it is not reused on refresh. */
export function consumeBelegcheckPrefillSession(): string | null {
  if (typeof window === 'undefined') return null;
  try {
    const v = sessionStorage.getItem(RKSV_BELEGCHECK_PREFILL_SESSION_KEY);
    sessionStorage.removeItem(RKSV_BELEGCHECK_PREFILL_SESSION_KEY);
    const t = v?.trim();
    return t ? t : null;
  } catch {
    return null;
  }
}
