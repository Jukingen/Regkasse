/**
 * Session-only preference: skip fiscal export disclaimer modal until timestamp (24h option).
 */

const STORAGE_KEY = 'regkasse.fiscalExport.disclaimerSkipUntil';

const TWENTY_FOUR_H_MS = 24 * 60 * 60 * 1000;

export function getFiscalExportDisclaimerSkipUntilMs(): number | null {
  if (typeof globalThis.sessionStorage === 'undefined') return null;
  const raw = globalThis.sessionStorage.getItem(STORAGE_KEY);
  if (raw == null || raw === '') return null;
  const n = Number.parseInt(raw, 10);
  return Number.isFinite(n) ? n : null;
}

export function isFiscalExportDisclaimerSkipped(): boolean {
  const until = getFiscalExportDisclaimerSkipUntilMs();
  return until != null && Date.now() < until;
}

export function setFiscalExportDisclaimerSkip24h(): void {
  if (typeof globalThis.sessionStorage === 'undefined') return;
  globalThis.sessionStorage.setItem(STORAGE_KEY, String(Date.now() + TWENTY_FOUR_H_MS));
}
