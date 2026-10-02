import { useCallback, useEffect, useState } from 'react';

import { OFFLINE_CONFIG } from '@/constants/offlineConfig';
import { getOfflineOrderManager } from '@/services/offline/offlineOrderManager';
import {
  countOfflineQueueByStatus,
  mergeOfflineQueueItems,
  type OfflineQueueListItem,
} from '@/services/offline/offlineQueueSnapshot';
import { OfflineSyncHistory } from '@/services/offline/offlineSyncHistory';
import { OfflineSyncService } from '@/services/offline/offlineSyncService';
import { fetchOfflineTenantLimitUsage } from '@/services/offline/offlineTenantLimit';
import { getAllQueueEntries, getPendingPaymentQueue } from '@/services/payment/pendingPaymentQueue';
import { eventEmitter } from '@/utils/eventEmitter';
import { fetchIsNetworkOnline } from '@/utils/isNetworkOnline';
import {
  offlineQueueActionableCount,
  resolveOfflineQueueDot,
  type OfflineQueueDotColor,
} from '@/utils/offlineQueueIndicator';
import {
  isOfflineQueueAtCap,
  mergeOfflineTenantLimit,
} from '@/utils/offlineQueueLimit';

export type OfflineQueueIndicatorState = {
  isOnline: boolean;
  isSyncing: boolean;
  pendingCount: number;
  failedCount: number;
  count: number;
  color: OfflineQueueDotColor;
  items: OfflineQueueListItem[];
  lastSuccessfulSyncAt: Date | null;
  nextRetryAt: Date | null;
  tenantLimit: { current: number; limit: number };
  atCap: boolean;
};

const EMPTY_STATE: OfflineQueueIndicatorState = {
  isOnline: true,
  isSyncing: false,
  pendingCount: 0,
  failedCount: 0,
  count: 0,
  color: 'green',
  items: [],
  lastSuccessfulSyncAt: null,
  nextRetryAt: null,
  tenantLimit: { current: 0, limit: OFFLINE_CONFIG.MAX_OFFLINE_TRANSACTIONS },
  atCap: false,
};

export function useOfflineQueueIndicator(): OfflineQueueIndicatorState & {
  refresh: () => void;
} {
  const [state, setState] = useState<OfflineQueueIndicatorState>(EMPTY_STATE);

  const refresh = useCallback(() => {
    void (async () => {
      const isOnline = await fetchIsNetworkOnline();
      const syncStatus = OfflineSyncService.getInstance().getSyncStatus();
      const [orders, payments, history, serverLimit, localPending] = await Promise.all([
        getOfflineOrderManager()
          .listAllOrders()
          .catch(() => []),
        getAllQueueEntries().catch(() => []),
        OfflineSyncHistory.getInstance().getHistory(50).catch(() => []),
        fetchOfflineTenantLimitUsage(),
        getPendingPaymentQueue().catch(() => []),
      ]);

      const items = mergeOfflineQueueItems(orders, payments);
      const { pendingCount, failedCount } = countOfflineQueueByStatus(items);
      const lastSuccess = history.find((row) => row.status === 'success');
      const lastSuccessfulSyncAt = lastSuccess?.timestamp ?? syncStatus.lastSyncAt;
      const nextRetryAt =
        !isOnline || (pendingCount === 0 && failedCount === 0)
          ? null
          : (syncStatus.nextSyncAt ??
            new Date(Date.now() + OFFLINE_CONFIG.SYNC_INTERVAL_SECONDS * 1000));

      const color = resolveOfflineQueueDot({
        isOnline,
        pendingCount,
        failedCount,
        isSyncing: syncStatus.isSyncing,
      });
      const tenantLimit = mergeOfflineTenantLimit(serverLimit, localPending.length);
      const atCap = isOfflineQueueAtCap(tenantLimit.current, tenantLimit.limit);

      setState({
        isOnline,
        isSyncing: syncStatus.isSyncing,
        pendingCount,
        failedCount,
        count: offlineQueueActionableCount(pendingCount, failedCount),
        color,
        items,
        lastSuccessfulSyncAt,
        nextRetryAt,
        tenantLimit,
        atCap,
      });
    })();
  }, []);

  useEffect(() => {
    refresh();
    const onChange = () => {
      refresh();
    };
    eventEmitter.on('sync:status', onChange);
    eventEmitter.on('sync:online', onChange);
    eventEmitter.on('sync:offline', onChange);
    eventEmitter.on('sync:completed', onChange);
    eventEmitter.on('sync:error', onChange);
    eventEmitter.on('offline:order-saved', onChange);

    const interval = setInterval(onChange, OFFLINE_CONFIG.STATUS_POLL_INTERVAL_SECONDS * 1000);
    return () => {
      eventEmitter.off('sync:status', onChange);
      eventEmitter.off('sync:online', onChange);
      eventEmitter.off('sync:offline', onChange);
      eventEmitter.off('sync:completed', onChange);
      eventEmitter.off('sync:error', onChange);
      eventEmitter.off('offline:order-saved', onChange);
      clearInterval(interval);
    };
  }, [refresh]);

  return { ...state, refresh };
}
