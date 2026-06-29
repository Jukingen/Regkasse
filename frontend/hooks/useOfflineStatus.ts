import NetInfo from '@react-native-community/netinfo';
import { useCallback, useEffect, useState } from 'react';

import { OfflineSessionManager } from '@/services/auth/offlineSessionManager';
import { OfflineConfigService } from '@/services/config/offlineConfigService';
import { OfflineSyncService } from '@/services/offline/offlineSyncService';
import { eventEmitter } from '@/utils/eventEmitter';

export interface OfflineStatus {
  isOnline: boolean;
  isSyncing: boolean;
  isTokenValid: boolean;
  canWorkOffline: boolean;
  pendingOrders: number;
  pendingPayments: number;
  remainingHours: number;
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

export function useOfflineStatus(): OfflineStatus & {
  syncNow: () => Promise<void>;
  refresh: () => void;
} {
  const config = OfflineConfigService.getInstance();
  const sessionManager = OfflineSessionManager.getInstance();
  const syncService = OfflineSyncService.getInstance();

  const [status, setStatus] = useState<OfflineStatus>({
    isOnline: true,
    isSyncing: false,
    isTokenValid: false,
    canWorkOffline: false,
    pendingOrders: 0,
    pendingPayments: 0,
    remainingHours: 0,
    tokenExpiryHours: config.get('TOKEN_EXPIRY_HOURS'),
    isExpiringSoon: false,
    lastSyncAt: null,
  });

  const refresh = useCallback(() => {
    void (async () => {
      const syncStatus = syncService.getSyncStatus();
      const remainingHours = sessionManager.getRemainingOfflineHours();
      const tokenValid = sessionManager.canWorkOffline();
      const isOnline = await resolveIsOnline();
      const warningHours = config.get('OFFLINE_WARNING_HOURS');

      setStatus({
        isOnline,
        isSyncing: syncStatus.isSyncing,
        isTokenValid: tokenValid,
        canWorkOffline: tokenValid || !isOnline,
        pendingOrders: syncStatus.pendingOrders,
        pendingPayments: syncStatus.pendingPayments,
        remainingHours,
        tokenExpiryHours: config.get('TOKEN_EXPIRY_HOURS'),
        isExpiringSoon: remainingHours > 0 && remainingHours < warningHours,
        lastSyncAt: syncStatus.lastSyncAt,
      });
    })();
  }, [config, sessionManager, syncService]);

  const syncNow = useCallback(async () => {
    await syncService.syncNow();
    refresh();
  }, [refresh, syncService]);

  useEffect(() => {
    refresh();

    const statusHandler = () => refresh();
    const onlineHandler = () => refresh();
    const offlineHandler = () => refresh();

    eventEmitter.on('sync:status', statusHandler);
    eventEmitter.on('sync:online', onlineHandler);
    eventEmitter.on('sync:offline', offlineHandler);

    const netInfoUnsubscribe = NetInfo.addEventListener(() => {
      refresh();
    });

    if (typeof window !== 'undefined') {
      window.addEventListener('online', onlineHandler);
      window.addEventListener('offline', offlineHandler);
    }

    const intervalMs = config.get('SYNC_INTERVAL_SECONDS') * 1000;
    const interval = setInterval(refresh, intervalMs);

    return () => {
      eventEmitter.off('sync:status', statusHandler);
      eventEmitter.off('sync:online', onlineHandler);
      eventEmitter.off('sync:offline', offlineHandler);
      netInfoUnsubscribe();

      if (typeof window !== 'undefined') {
        window.removeEventListener('online', onlineHandler);
        window.removeEventListener('offline', offlineHandler);
      }

      clearInterval(interval);
    };
  }, [config, refresh]);

  return { ...status, syncNow, refresh };
}
