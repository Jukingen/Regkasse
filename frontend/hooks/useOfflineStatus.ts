import NetInfo from '@react-native-community/netinfo';
import { useCallback, useEffect, useState } from 'react';

import { OfflineSessionManager } from '@/services/auth/offlineSessionManager';
import { OfflineConfigService } from '@/services/config/offlineConfigService';
import { OfflineOrderManager } from '@/services/offline/offlineOrderManager';
import { OfflineSyncService } from '@/services/offline/offlineSyncService';
import { eventEmitter } from '@/utils/eventEmitter';
import {
  resolveOfflineUiLevel,
  type OfflineUiLevel,
} from '@/utils/offlineStatusLevel';

export interface OfflineStatus {
  isOnline: boolean;
  isSyncing: boolean;
  isTokenValid: boolean;
  canWorkOffline: boolean;
  pendingOrders: number;
  /** Alias for pendingOrders — OfflineCounter / capacity UI. */
  pendingCount: number;
  pendingPayments: number;
  /** Hours until soonest pending order expires (or full window when empty). */
  remainingHours: number;
  /** Alias for remainingHours. */
  hoursRemaining: number;
  /** Max offline capacity shown in POS counter (RKSV-style 50). */
  maxLimit: number;
  remainingCapacity: number;
  statusLevel: OfflineUiLevel;
  tokenExpiryHours: number;
  isExpiringSoon: boolean;
  lastSyncAt: Date | null;
}

async function resolveIsOnline(): Promise<boolean> {
  try {
    const state = await NetInfo.fetch();
    return state.isConnected === true && state.isInternetReachable !== false;
  } catch {
    if (typeof navigator !== 'undefined' && 'onLine' in navigator) {
      return navigator.onLine;
    }
    return true;
  }
}

/**
 * Live offline queue status for POS (capacity, expiry window, connectivity).
 * Polls every STATUS_POLL_INTERVAL_SECONDS and refreshes on sync / network events.
 */
export function useOfflineStatus(): OfflineStatus & {
  syncNow: () => Promise<void>;
  refresh: () => void;
} {
  const config = OfflineConfigService.getInstance();
  const sessionManager = OfflineSessionManager.getInstance();
  const syncService = OfflineSyncService.getInstance();
  const orderManager = OfflineOrderManager.getInstance({ autoSync: false });

  const maxLimit = config.get('MAX_OFFLINE_TRANSACTIONS');
  const expiryHours = config.get('OFFLINE_EXPIRY_HOURS');

  const [status, setStatus] = useState<OfflineStatus>({
    isOnline: true,
    isSyncing: false,
    isTokenValid: false,
    canWorkOffline: false,
    pendingOrders: 0,
    pendingCount: 0,
    pendingPayments: 0,
    remainingHours: expiryHours,
    hoursRemaining: expiryHours,
    maxLimit,
    remainingCapacity: maxLimit,
    statusLevel: 'online',
    tokenExpiryHours: config.get('TOKEN_EXPIRY_HOURS'),
    isExpiringSoon: false,
    lastSyncAt: null,
  });

  const refresh = useCallback(() => {
    void (async () => {
      const syncStatus = syncService.getSyncStatus();
      const tokenRemainingHours = sessionManager.getRemainingOfflineHours();
      const tokenValid = sessionManager.canWorkOffline();
      const isOnline = await resolveIsOnline();
      const warningHours = config.get('OFFLINE_WARNING_HOURS');
      const limit = config.get('MAX_OFFLINE_TRANSACTIONS');
      const fallbackHours = config.get('OFFLINE_EXPIRY_HOURS');

      let pendingCount = syncStatus.pendingOrders;
      let hoursRemaining = fallbackHours;

      try {
        pendingCount = await orderManager.getPendingCount();
        hoursRemaining = await orderManager.getHoursRemaining(fallbackHours);
      } catch {
        // Keep syncStatus pending count if local storage read fails.
      }

      const remainingCapacity = Math.max(0, limit - pendingCount);
      const statusLevel = resolveOfflineUiLevel({
        isOnline,
        pendingCount,
        hoursRemaining,
        maxLimit: limit,
        warningPendingCount: config.get('WARNING_PENDING_COUNT'),
        criticalPendingCount: config.get('CRITICAL_PENDING_COUNT'),
        warningHours,
      });

      setStatus({
        isOnline,
        isSyncing: syncStatus.isSyncing,
        isTokenValid: tokenValid,
        canWorkOffline: tokenValid || !isOnline,
        pendingOrders: pendingCount,
        pendingCount,
        pendingPayments: syncStatus.pendingPayments,
        remainingHours: hoursRemaining,
        hoursRemaining,
        maxLimit: limit,
        remainingCapacity,
        statusLevel,
        tokenExpiryHours: config.get('TOKEN_EXPIRY_HOURS'),
        isExpiringSoon:
          (pendingCount > 0 && hoursRemaining > 0 && hoursRemaining < warningHours) ||
          (tokenRemainingHours > 0 && tokenRemainingHours < warningHours),
        lastSyncAt: syncStatus.lastSyncAt,
      });
    })();
  }, [config, orderManager, sessionManager, syncService]);

  const syncNow = useCallback(async () => {
    await syncService.syncNow();
    refresh();
  }, [refresh, syncService]);

  useEffect(() => {
    refresh();

    const updateStatus = () => refresh();

    eventEmitter.on('sync:status', updateStatus);
    eventEmitter.on('sync:online', updateStatus);
    eventEmitter.on('sync:offline', updateStatus);
    eventEmitter.on('offline:order-saved', updateStatus);
    eventEmitter.on('offline:limit-exceeded', updateStatus);

    const netInfoUnsubscribe = NetInfo.addEventListener(updateStatus);

    if (typeof window !== 'undefined') {
      window.addEventListener('online', updateStatus);
      window.addEventListener('offline', updateStatus);
    }

    // Default 5s — OFFLINE_CONFIG.STATUS_POLL_INTERVAL_SECONDS
    const intervalMs = config.get('STATUS_POLL_INTERVAL_SECONDS') * 1000;
    const interval = setInterval(updateStatus, intervalMs);

    return () => {
      eventEmitter.off('sync:status', updateStatus);
      eventEmitter.off('sync:online', updateStatus);
      eventEmitter.off('sync:offline', updateStatus);
      eventEmitter.off('offline:order-saved', updateStatus);
      eventEmitter.off('offline:limit-exceeded', updateStatus);
      netInfoUnsubscribe();

      if (typeof window !== 'undefined') {
        window.removeEventListener('online', updateStatus);
        window.removeEventListener('offline', updateStatus);
      }

      clearInterval(interval);
    };
  }, [config, refresh]);

  return { ...status, syncNow, refresh };
}
