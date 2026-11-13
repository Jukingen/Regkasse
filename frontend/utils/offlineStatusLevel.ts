import { OFFLINE_CONFIG } from '@/constants/offlineConfig';

/** Color-coded POS offline indicator levels (German UI maps labels separately). */
export type OfflineUiLevel = 'online' | 'warning' | 'critical' | 'offline';

export type ResolveOfflineUiLevelInput = {
  isOnline: boolean;
  pendingCount: number;
  /** Hours until the soonest pending offline order expires (72h window). */
  hoursRemaining: number;
  maxLimit?: number;
  warningPendingCount?: number;
  criticalPendingCount?: number;
  warningHours?: number;
};

/**
 * Resolve POS offline status color level.
 * - offline: no connection
 * - critical: ≥48 pending OR (pending > 0 and &lt;24h remaining)
 * - warning: ≥40 pending
 * - online: otherwise
 */
export function resolveOfflineUiLevel(input: ResolveOfflineUiLevelInput): OfflineUiLevel {
  const maxLimit = input.maxLimit ?? OFFLINE_CONFIG.MAX_OFFLINE_TRANSACTIONS;
  const warningAt = input.warningPendingCount ?? OFFLINE_CONFIG.WARNING_PENDING_COUNT;
  const criticalAt = input.criticalPendingCount ?? OFFLINE_CONFIG.CRITICAL_PENDING_COUNT;
  const warningHours = input.warningHours ?? OFFLINE_CONFIG.OFFLINE_WARNING_HOURS;

  if (!input.isOnline) {
    return 'offline';
  }

  const pending = Math.max(0, input.pendingCount);
  const hours = Math.max(0, input.hoursRemaining);
  const approachingCap = pending >= Math.min(criticalAt, maxLimit);
  const timeCritical = pending > 0 && hours < warningHours;

  if (approachingCap || timeCritical) {
    return 'critical';
  }

  if (pending >= warningAt) {
    return 'warning';
  }

  return 'online';
}

export const OFFLINE_UI_LEVEL_COLORS: Record<OfflineUiLevel, string> = {
  online: '#16a34a',
  warning: '#eab308',
  critical: '#dc2626',
  offline: '#111827',
};
