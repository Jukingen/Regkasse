import { useCallback, useEffect, useState } from 'react';

import {
  OfflineSyncService,
  type SyncAllResult,
} from '@/services/offline/offlineSyncService';
import { eventEmitter } from '@/utils/eventEmitter';

import {
  getOfflineOrderManager,
  type OfflineStatus,
} from '../services/offline/offlineOrderManager';
import type { OfflineOrder } from '../services/offline/offlineStorage';

function mapSyncStatusToOfflineStatus(
  syncStatus: ReturnType<OfflineSyncService['getSyncStatus']>,
  isOnline: boolean
): OfflineStatus {
  return {
    isOnline,
    pendingCount: syncStatus.pendingOrders,
    isSyncing: syncStatus.isSyncing,
    oldestPending: null,
  };
}

export function useOfflineOrderManager() {
  const [status, setStatus] = useState<OfflineStatus | undefined>();
  const [manager] = useState(() => getOfflineOrderManager());
  const [syncService] = useState(() => OfflineSyncService.getInstance());

  const refreshStatus = useCallback(async () => {
    try {
      const managerStatus = await manager.getStatus();
      const syncStatus = syncService.getSyncStatus();
      setStatus({
        ...managerStatus,
        pendingCount: syncStatus.pendingOrders,
        isSyncing: syncStatus.isSyncing,
      });
    } catch (err) {
      console.warn('[useOfflineOrderManager] Status refresh failed:', err);
    }
  }, [manager, syncService]);

  useEffect(() => {
    void refreshStatus();

    const onSyncStatus = (
      syncStatus: ReturnType<OfflineSyncService['getSyncStatus']>
    ) => {
      void (async () => {
        try {
          const managerStatus = await manager.getStatus();
          setStatus({
            ...managerStatus,
            pendingCount: syncStatus.pendingOrders,
            isSyncing: syncStatus.isSyncing,
          });
        } catch {
          setStatus(mapSyncStatusToOfflineStatus(syncStatus, true));
        }
      })();
    };

    const onConnectivityChange = () => {
      void refreshStatus();
    };

    eventEmitter.on('sync:status', onSyncStatus);
    eventEmitter.on('sync:online', onConnectivityChange);
    eventEmitter.on('sync:offline', onConnectivityChange);

    return () => {
      eventEmitter.off('sync:status', onSyncStatus);
      eventEmitter.off('sync:online', onConnectivityChange);
      eventEmitter.off('sync:offline', onConnectivityChange);
    };
  }, [manager, refreshStatus]);

  const saveOrder = useCallback(
    async (orderData: unknown, paymentMethod: string): Promise<OfflineOrder> => {
      const order = await manager.saveOrder(orderData, paymentMethod);
      await refreshStatus();
      return order;
    },
    [manager, refreshStatus]
  );

  const syncNow = useCallback(async (): Promise<SyncAllResult> => {
    const result = await syncService.syncNow();
    await refreshStatus();
    return result;
  }, [refreshStatus, syncService]);

  const getPending = useCallback(
    () => manager.getPendingOrders(),
    [manager]
  );

  return {
    status,
    saveOrder,
    syncNow,
    getPending,
  };
}
