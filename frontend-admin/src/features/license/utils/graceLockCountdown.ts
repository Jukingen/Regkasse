import { getLicenseExpiryCountdownParts } from '@/features/license/utils/licenseExpiryCountdown';

const MINUTE_MS = 60 * 1000;

/**
 * Live countdown until grace lockdown. Under 24h includes seconds for urgency UI.
 * Returns null when lock time is unknown; empty-passed when already locked.
 */
export function formatGraceLockCountdown(
  graceEndedAt: string | null | undefined,
  nowMs = Date.now()
): string | null {
  if (!graceEndedAt?.trim()) return null;

  const parts = getLicenseExpiryCountdownParts(graceEndedAt, nowMs);
  if (!parts) return null;
  if (parts.totalMs <= 0) return '0h 0m 0s';

  const seconds = Math.floor((parts.totalMs % MINUTE_MS) / 1000);
  if (parts.days > 0) {
    return `${parts.days}d ${parts.hours}h ${parts.minutes}m`;
  }
  return `${parts.hours}h ${parts.minutes}m ${seconds}s`;
}
