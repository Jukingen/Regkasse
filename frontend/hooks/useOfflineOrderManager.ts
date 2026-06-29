import { useCallback, useEffect, useState } from 'react';
import {
  getOfflineOrderManager,
  type OfflineStatus,
  type SyncResult,
} from '../services/offline/offlineOrderManager';
import type { OfflineOrder } from '../services/offline/offlineStorage';

export function useOfflineOrderManager() {
  const [status, setStatus] = useState<OfflineStatus | undefined>();
  const [manager] = useState(() => getOfflineOrderManager());

  const refreshStatus = useCallback(async () => {
    try {
      setStatus(await manager.getStatus());
    } catch (err) {
      console.warn('[useOfflineOrderManager] Status refresh failed:', err);
    }
  }, [manager]);

  useEffect(() => {
    void refreshStatus();

    const interval = setInterval(() => {
      void refreshStatus();
    }, 5000);

    return () => clearInterval(interval);
  }, [refreshStatus]);

  const saveOrder = useCallback(
    async (orderData: unknown, paymentMethod: string): Promise<OfflineOrder> => {
      const order = await manager.saveOrder(orderData, paymentMethod);
      await refreshStatus();
      return order;
    },
    [manager, refreshStatus]
  );

  const syncNow = useCallback(async (): Promise<SyncResult> => {
    const result = await manager.syncPendingOrders();
    await refreshStatus();
    return result;
  }, [manager, refreshStatus]);

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
