/**
 * Progress percent for remaining grace days within the configured grace window.
 * Full bar = full grace remaining; empty = grace exhausted.
 */
export function getGracePeriodProgressPercent(
  graceDaysRemaining: number,
  gracePeriodDays: number
): number {
  if (!Number.isFinite(graceDaysRemaining) || !Number.isFinite(gracePeriodDays)) {
    return 0;
  }
  if (gracePeriodDays <= 0) return 0;
  return Math.max(0, Math.min(100, (graceDaysRemaining / gracePeriodDays) * 100));
}

/**
 * Progress percent for consumed grace days (fills as lockdown approaches).
 * Empty = just entered grace; full = grace exhausted.
 */
export function getGracePeriodConsumedPercent(
  graceDaysRemaining: number,
  gracePeriodDays: number
): number {
  if (!Number.isFinite(graceDaysRemaining) || !Number.isFinite(gracePeriodDays)) {
    return 0;
  }
  if (gracePeriodDays <= 0) return 0;
  const remaining = Math.max(0, Math.min(gracePeriodDays, graceDaysRemaining));
  return Math.max(0, Math.min(100, ((gracePeriodDays - remaining) / gracePeriodDays) * 100));
}
