/** Accidental-closure recovery for mandant license renewal (localStorage, 1h TTL). */

export const LICENSE_RENEWAL_RECOVERY_TTL_MS = 60 * 60 * 1000;

const PENDING_PREFIX = 'regkasse.license.renewalPending.';
const STARTED_PREFIX = 'regkasse.license.renewalStartedAt.';

/** Sketch-compatible global keys (fallback when tenant id unknown). */
const LEGACY_PENDING_KEY = 'licenseRenewalPending';
const LEGACY_STARTED_KEY = 'licenseRenewalStartedAt';

function pendingKey(tenantId: string): string {
  return `${PENDING_PREFIX}${tenantId}`;
}

function startedKey(tenantId: string): string {
  return `${STARTED_PREFIX}${tenantId}`;
}

function canUseLocalStorage(): boolean {
  return typeof globalThis.localStorage !== 'undefined';
}

export function markLicenseRenewalPending(
  tenantId: string,
  startedAtIso = new Date().toISOString()
): void {
  if (!canUseLocalStorage() || !tenantId.trim()) return;
  try {
    globalThis.localStorage.setItem(pendingKey(tenantId), 'true');
    globalThis.localStorage.setItem(startedKey(tenantId), startedAtIso);
    // Keep sketch keys in sync for the active tenant session.
    globalThis.localStorage.setItem(LEGACY_PENDING_KEY, 'true');
    globalThis.localStorage.setItem(LEGACY_STARTED_KEY, startedAtIso);
  } catch {
    // ignore quota / private mode
  }
}

export function clearLicenseRenewalPending(tenantId?: string | null): void {
  if (!canUseLocalStorage()) return;
  try {
    if (tenantId?.trim()) {
      globalThis.localStorage.removeItem(pendingKey(tenantId));
      globalThis.localStorage.removeItem(startedKey(tenantId));
    }
    globalThis.localStorage.removeItem(LEGACY_PENDING_KEY);
    globalThis.localStorage.removeItem(LEGACY_STARTED_KEY);
  } catch {
    // ignore
  }
}

export function isLicenseRenewalPending(
  tenantId: string,
  nowMs = Date.now(),
  ttlMs = LICENSE_RENEWAL_RECOVERY_TTL_MS
): boolean {
  if (!canUseLocalStorage() || !tenantId.trim()) return false;
  try {
    const pending =
      globalThis.localStorage.getItem(pendingKey(tenantId)) ??
      globalThis.localStorage.getItem(LEGACY_PENDING_KEY);
    if (pending !== 'true') return false;

    const startedRaw =
      globalThis.localStorage.getItem(startedKey(tenantId)) ??
      globalThis.localStorage.getItem(LEGACY_STARTED_KEY);
    if (!startedRaw?.trim()) {
      clearLicenseRenewalPending(tenantId);
      return false;
    }

    const startedMs = Date.parse(startedRaw);
    if (!Number.isFinite(startedMs)) {
      clearLicenseRenewalPending(tenantId);
      return false;
    }

    if (nowMs - startedMs >= ttlMs) {
      clearLicenseRenewalPending(tenantId);
      return false;
    }

    return true;
  } catch {
    return false;
  }
}
