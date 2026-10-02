/** POS queue-dot colors (operator header). Separate from capacity `offlineStatusLevel`. */

export type OfflineQueueDotColor = 'green' | 'yellow' | 'orange' | 'red';

export type ResolveOfflineQueueDotInput = {
  isOnline: boolean;
  pendingCount: number;
  failedCount: number;
  isSyncing: boolean;
};

export const OFFLINE_QUEUE_DOT_COLORS: Record<OfflineQueueDotColor, string> = {
  green: '#16a34a',
  yellow: '#eab308',
  orange: '#f97316',
  red: '#dc2626',
};

/**
 * Header indicator:
 * - green: online, queue empty
 * - yellow: online, N items (syncing or waiting)
 * - orange: offline, N items
 * - red: sync failed, N items stuck
 */
export function resolveOfflineQueueDot(input: ResolveOfflineQueueDotInput): OfflineQueueDotColor {
  const pending = Math.max(0, input.pendingCount);
  const failed = Math.max(0, input.failedCount);

  if (failed > 0) {
    return 'red';
  }
  if (!input.isOnline) {
    return 'orange';
  }
  if (pending > 0 || input.isSyncing) {
    return 'yellow';
  }
  return 'green';
}

export function offlineQueueActionableCount(pendingCount: number, failedCount: number): number {
  return Math.max(0, pendingCount) + Math.max(0, failedCount);
}

export function formatRetryCountdownMs(nextRetryAt: Date | null, now: Date): number | null {
  if (!nextRetryAt) return null;
  const ms = nextRetryAt.getTime() - now.getTime();
  return ms > 0 ? ms : 0;
}

export function formatRetryCountdownLabel(ms: number | null): string | null {
  if (ms == null) return null;
  const totalSeconds = Math.max(0, Math.ceil(ms / 1000));
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  if (minutes <= 0) {
    return `${seconds}s`;
  }
  return `${minutes}m ${String(seconds).padStart(2, '0')}s`;
}
