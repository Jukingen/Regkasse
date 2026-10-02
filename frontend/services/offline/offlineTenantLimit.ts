import { apiClient } from '@/services/api/config';
import { OFFLINE_CONFIG } from '@/constants/offlineConfig';
import { mergeOfflineTenantLimit, offlineLimitPercent, isOfflineQueueAtCap } from '@/utils/offlineQueueLimit';
import type { OfflineTenantLimitUsage } from '@/utils/offlineQueueLimit';

let cached: OfflineTenantLimitUsage | null = null;

function asNumber(value: unknown, fallback: number): number {
  const n = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(n) ? n : fallback;
}

function asBool(value: unknown): boolean | undefined {
  if (typeof value === 'boolean') return value;
  if (typeof value === 'string') {
    if (value.toLowerCase() === 'true') return true;
    if (value.toLowerCase() === 'false') return false;
  }
  return undefined;
}

function pickUsage(payload: Record<string, unknown> | null | undefined): OfflineTenantLimitUsage | null {
  if (!payload) return null;
  const nested = payload.data;
  const row =
    nested && typeof nested === 'object' ? (nested as Record<string, unknown>) : payload;
  const hasCurrent =
    'currentOfflineTransactions' in row || 'CurrentOfflineTransactions' in row;
  const hasLimit = 'maxOfflineTransactions' in row || 'MaxOfflineTransactions' in row;
  if (!hasCurrent && !hasLimit) return null;
  const current = Math.max(
    0,
    asNumber(row.currentOfflineTransactions ?? row.CurrentOfflineTransactions, 0)
  );
  const limit = Math.max(
    1,
    asNumber(
      row.maxOfflineTransactions ?? row.MaxOfflineTransactions,
      OFFLINE_CONFIG.MAX_OFFLINE_TRANSACTIONS
    )
  );
  const approaching =
    asBool(row.approachingLimit ?? row.ApproachingLimit) ?? offlineLimitPercent(current, limit) >= 80;
  const reached =
    asBool(row.limitReached ?? row.LimitReached) ?? isOfflineQueueAtCap(current, limit);
  return {
    current,
    limit,
    approachingLimit: approaching,
    limitReached: reached,
  };
}

export function getCachedOfflineTenantLimit(): OfflineTenantLimitUsage | null {
  return cached;
}

export async function fetchOfflineTenantLimitUsage(): Promise<OfflineTenantLimitUsage | null> {
  try {
    const path = OFFLINE_CONFIG.SYNC_ENDPOINTS.LIMIT.replace(/^\/api/, '');
    const raw = await apiClient.get<unknown>(path);
    const usage = pickUsage(
      raw && typeof raw === 'object' ? (raw as Record<string, unknown>) : null
    );
    if (usage) cached = usage;
    return cached;
  } catch {
    return cached;
  }
}

export async function resolveEffectiveOfflineTenantLimit(
  localPendingCount: number
): Promise<OfflineTenantLimitUsage> {
  const server = await fetchOfflineTenantLimitUsage();
  return mergeOfflineTenantLimit(server, localPendingCount);
}
