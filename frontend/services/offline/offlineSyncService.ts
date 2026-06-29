import NetInfo from '@react-native-community/netinfo';

import { OfflineSessionManager } from '@/services/auth/offlineSessionManager';
import { OfflineConfigService } from '@/services/config/offlineConfigService';
import { eventEmitter } from '@/utils/eventEmitter';

import { OfflineOrderManager } from './offlineOrderManager';

export interface SyncStatus {
  isSyncing: boolean;
  pendingOrders: number;
  pendingPayments: number;
  lastSyncAt: Date | null;
  nextSyncAt: Date | null;
}

export type SyncAllResult = {
  success: boolean;
  synced: number;
  errors: number;
};

function countSyncResults(details: Array<{ success: boolean }> | undefined): {
  synced: number;
  errors: number;
} {
  if (!details?.length) {
    return { synced: 0, errors: 0 };
  }
  const synced = details.filter((row) => row.success).length;
  return { synced, errors: details.length - synced };
}

export class OfflineSyncService {
  private static instance: OfflineSyncService | undefined;
  private readonly config: OfflineConfigService;
  private readonly sessionManager: OfflineSessionManager;
  private readonly orderManager: OfflineOrderManager;
  private syncInterval: ReturnType<typeof setInterval> | null = null;
  private netInfoUnsubscribe: (() => void) | null = null;
  private isSyncing = false;
  private lastSyncAt: Date | null = null;
  private pendingOrdersCount = 0;

  private constructor() {
    this.config = OfflineConfigService.getInstance();
    this.sessionManager = OfflineSessionManager.getInstance();
    this.orderManager = OfflineOrderManager.getInstance({ autoSync: false });
    this.setupListeners();
    void this.refreshPendingCounts();
    this.startAutoSync();
  }

  static getInstance(): OfflineSyncService {
    if (!OfflineSyncService.instance) {
      OfflineSyncService.instance = new OfflineSyncService();
    }
    return OfflineSyncService.instance;
  }

  private startAutoSync(): void {
    const intervalMs = this.config.get('SYNC_INTERVAL_SECONDS') * 1000;

    this.syncInterval = setInterval(() => {
      void this.autoSync();
    }, intervalMs);
  }

  private async autoSync(): Promise<void> {
    if (!(await this.isOnline()) || this.isSyncing) return;
    if (!this.sessionManager.canWorkOffline()) return;

    await this.syncAll();
  }

  async syncAll(): Promise<SyncAllResult> {
    if (this.isSyncing) {
      return { success: false, synced: 0, errors: 0 };
    }

    this.isSyncing = true;
    this.emitStatusChange();

    try {
      const orderResult = await this.orderManager.syncPendingOrders();
      const { synced, errors } = countSyncResults(orderResult.details);

      this.lastSyncAt = new Date();
      await this.refreshPendingCounts();
      this.emitStatusChange();

      if (synced > 0) {
        eventEmitter.emit('sync:completed', { synced, errors });
      }

      return {
        success: errors === 0 && orderResult.success,
        synced,
        errors,
      };
    } catch (error) {
      eventEmitter.emit(
        'sync:error',
        error instanceof Error ? error : new Error('Offline sync failed')
      );

      return {
        success: false,
        synced: 0,
        errors: 1,
      };
    } finally {
      this.isSyncing = false;
      this.emitStatusChange();
    }
  }

  async syncNow(): Promise<SyncAllResult> {
    if (!(await this.isOnline())) {
      eventEmitter.emit('sync:error', new Error('No internet connection'));
      return { success: false, synced: 0, errors: 1 };
    }

    return this.syncAll();
  }

  private async isOnline(): Promise<boolean> {
    try {
      const state = await NetInfo.fetch();
      return state.isConnected === true && state.isInternetReachable !== false;
    } catch {
      if (typeof navigator !== 'undefined' && 'onLine' in navigator) {
        return navigator.onLine;
      }
      return false;
    }
  }

  getSyncStatus(): SyncStatus {
    return {
      isSyncing: this.isSyncing,
      pendingOrders: this.pendingOrdersCount,
      pendingPayments: 0,
      lastSyncAt: this.lastSyncAt,
      nextSyncAt: this.isSyncing
        ? null
        : new Date(Date.now() + this.config.get('SYNC_INTERVAL_SECONDS') * 1000),
    };
  }

  private emitStatusChange(): void {
    eventEmitter.emit('sync:status', this.getSyncStatus());
  }

  private setupListeners(): void {
    this.netInfoUnsubscribe = NetInfo.addEventListener((state) => {
      const online =
        state.isConnected === true && state.isInternetReachable !== false;

      if (online) {
        eventEmitter.emit('sync:online');
        void this.autoSync();
        return;
      }

      eventEmitter.emit('sync:offline');
    });

    if (typeof window !== 'undefined') {
      window.addEventListener('online', () => {
        eventEmitter.emit('sync:online');
        void this.autoSync();
      });

      window.addEventListener('offline', () => {
        eventEmitter.emit('sync:offline');
      });
    }
  }

  private async refreshPendingCounts(): Promise<void> {
    try {
      const pending = await this.orderManager.getPendingOrders();
      this.pendingOrdersCount = pending.length;
    } catch {
      this.pendingOrdersCount = 0;
    }
  }

  destroy(): void {
    if (this.syncInterval) {
      clearInterval(this.syncInterval);
      this.syncInterval = null;
    }

    this.netInfoUnsubscribe?.();
    this.netInfoUnsubscribe = null;
  }

  static resetForTests(): void {
    OfflineSyncService.instance?.destroy();
    OfflineSyncService.instance = undefined;
  }
}
