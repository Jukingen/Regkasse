import type { LicenseStatusView } from '@/hooks/useLicenseStatus';

/** Banner escalates to error styling when this many grace days (or fewer) remain. */
export const GRACE_BANNER_URGENT_DAYS = 2;

/** Final grace day (≤24h until lockdown) — triggers blocking urgent warning. */
export function shouldShowGraceUrgentWarning(
  status: Pick<LicenseStatusView, 'state' | 'graceDaysRemaining'> | null | undefined
): boolean {
  if (!status || status.state !== 'Grace') return false;
  return status.graceDaysRemaining <= 1;
}

/** Non-blocking grace banner uses stronger styling in the last N days. */
export function isGracePeriodBannerUrgent(
  status: Pick<LicenseStatusView, 'state' | 'graceDaysRemaining'> | null | undefined
): boolean {
  if (!status || status.state !== 'Grace') return false;
  return status.graceDaysRemaining <= GRACE_BANNER_URGENT_DAYS;
}

export function graceUrgentDismissStorageKey(
  tenantId: string,
  graceEndedAt: string | null | undefined
): string {
  const deadline = graceEndedAt?.trim() || 'unknown';
  return `regkasse.license.graceUrgentDismissed.${tenantId}.${deadline}`;
}

export function isGraceUrgentDismissed(storageKey: string): boolean {
  if (typeof sessionStorage === 'undefined') return false;
  try {
    return sessionStorage.getItem(storageKey) === '1';
  } catch {
    return false;
  }
}

export function setGraceUrgentDismissed(storageKey: string): void {
  if (typeof sessionStorage === 'undefined') return;
  try {
    sessionStorage.setItem(storageKey, '1');
  } catch {
    // ignore quota / private mode
  }
}
