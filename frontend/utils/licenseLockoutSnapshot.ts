/**
 * Last mandant license lockout details (survives session clear until consumed/cleared).
 * Used by the POS license-expired screen after login gate denial or mid-session lockdown.
 */

import { storage } from './storage';

export const LICENSE_LOCKOUT_SNAPSHOT_KEY = 'regkasse.pos.licenseLockoutSnapshot';

export type LicenseLockoutSnapshot = {
  daysOverdue: number;
  savedAtMs: number;
};

const MAX_AGE_MS = 24 * 60 * 60 * 1000;

function normalizeDaysOverdue(raw: unknown): number {
  if (typeof raw !== 'number' || !Number.isFinite(raw)) return 0;
  return Math.max(0, Math.trunc(raw));
}

export function parseLicenseLockoutSnapshot(raw: unknown): LicenseLockoutSnapshot | null {
  if (!raw || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const savedAtMs =
    typeof o.savedAtMs === 'number' && Number.isFinite(o.savedAtMs) ? o.savedAtMs : NaN;
  if (!Number.isFinite(savedAtMs)) return null;
  return {
    daysOverdue: normalizeDaysOverdue(o.daysOverdue),
    savedAtMs,
  };
}

export function isLicenseLockoutSnapshotFresh(
  snapshot: LicenseLockoutSnapshot,
  nowMs: number = Date.now()
): boolean {
  return nowMs - snapshot.savedAtMs <= MAX_AGE_MS;
}

export async function saveLicenseLockoutSnapshot(daysOverdue: number): Promise<void> {
  const payload: LicenseLockoutSnapshot = {
    daysOverdue: normalizeDaysOverdue(daysOverdue),
    savedAtMs: Date.now(),
  };
  try {
    await storage.setItem(LICENSE_LOCKOUT_SNAPSHOT_KEY, JSON.stringify(payload));
  } catch {
    // ignore storage failures
  }
}

export async function loadLicenseLockoutSnapshot(): Promise<LicenseLockoutSnapshot | null> {
  try {
    const raw = await storage.getItem(LICENSE_LOCKOUT_SNAPSHOT_KEY);
    if (!raw?.trim()) return null;
    const parsed = parseLicenseLockoutSnapshot(JSON.parse(raw) as unknown);
    if (!parsed || !isLicenseLockoutSnapshotFresh(parsed)) {
      await clearLicenseLockoutSnapshot();
      return null;
    }
    return parsed;
  } catch {
    return null;
  }
}

export async function clearLicenseLockoutSnapshot(): Promise<void> {
  try {
    await storage.removeItem(LICENSE_LOCKOUT_SNAPSHOT_KEY);
  } catch {
    // ignore
  }
}
