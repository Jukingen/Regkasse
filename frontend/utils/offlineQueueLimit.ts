import { OFFLINE_CONFIG } from '@/constants/offlineConfig';

export type OfflineTenantLimitUsage = {
  current: number;
  limit: number;
  approachingLimit: boolean;
  limitReached: boolean;
};

export type OfflineLimitBarTone = 'green' | 'yellow' | 'red';

export function offlineLimitPercent(current: number, limit: number): number {
  if (limit <= 0) return 0;
  return Math.max(0, (current / limit) * 100);
}

export function resolveOfflineLimitBarTone(percent: number): OfflineLimitBarTone {
  if (percent >= 100) return 'red';
  if (percent >= 80) return 'yellow';
  return 'green';
}

export function isOfflineQueueAtCap(current: number, limit: number): boolean {
  return limit > 0 && current >= limit;
}

export function isOfflineQueueLimitFailure(opts: {
  error?: string;
  limitKey?: string;
  message?: string;
  status?: number;
}): boolean {
  if (opts.status === 409 && opts.error === 'LIMIT_EXCEEDED' && !opts.limitKey) {
    const msg = (opts.message ?? '').toLowerCase();
    if (msg.includes('offline') || msg.includes('warteschlange')) return true;
  }
  if (opts.limitKey === 'maxOfflineTransactions') return true;
  if (opts.error !== 'LIMIT_EXCEEDED' || opts.limitKey) return false;
  const msg = (opts.message ?? '').toLowerCase();
  return msg.includes('offline') || msg.includes('warteschlange');
}

export function shouldShowOfflineLimitReachedModal(opts: {
  limitReached?: boolean;
  error?: string;
  limitKey?: string;
  message?: string;
  status?: number;
}): boolean {
  if (opts.limitReached) return true;
  return isOfflineQueueLimitFailure(opts);
}

export const OFFLINE_LIMIT_BAR_COLORS: Record<OfflineLimitBarTone, string> = {
  green: '#16a34a',
  yellow: '#eab308',
  red: '#dc2626',
};

export function fallbackOfflineTenantLimit(localPendingCount: number): OfflineTenantLimitUsage {
  const current = Math.max(0, localPendingCount);
  const limit = OFFLINE_CONFIG.MAX_OFFLINE_TRANSACTIONS;
  return {
    current,
    limit,
    approachingLimit: offlineLimitPercent(current, limit) >= 80,
    limitReached: isOfflineQueueAtCap(current, limit),
  };
}

export function mergeOfflineTenantLimit(
  server: OfflineTenantLimitUsage | null,
  localPendingCount: number
): OfflineTenantLimitUsage {
  if (!server) {
    return fallbackOfflineTenantLimit(localPendingCount);
  }
  const current = Math.max(server.current, localPendingCount);
  const limit = Math.max(1, server.limit);
  return {
    current,
    limit,
    approachingLimit: server.approachingLimit || offlineLimitPercent(current, limit) >= 80,
    limitReached: server.limitReached || isOfflineQueueAtCap(current, limit),
  };
}
