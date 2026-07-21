import NetInfo from '@react-native-community/netinfo';

import { OfflineOrderManager } from './offlineOrderManager';
import { OfflineSyncHistory } from './offlineSyncHistory';

import { apiClient } from '@/services/api/config';
import { OfflineSessionManager } from '@/services/auth/offlineSessionManager';
import { OfflineConfigService } from '@/services/config/offlineConfigService';
import { sessionManager } from '@/services/session/sessionManager';
import { eventEmitter } from '@/utils/eventEmitter';
import { fetchIsNetworkOnline, isNetworkOnline } from '@/utils/isNetworkOnline';

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

type ServerSyncHealth = {
  pendingOrders: number;
  maxPending: number;
  isHealthy: boolean;
  status: 'healthy' | 'warning';
  lastSyncAt: string | null;
};

function countSyncResults(details: { success: boolean }[] | undefined): {
  synced: number;
  errors: number;
} {
  if (!details?.length) {
    return { synced: 0, errors: 0 };
  }
  const synced = details.filter((row) => row.success).length;
  return { synced, errors: details.length - synced };
}

type NetworkInformationLike = EventTarget & {
  effectiveType?: string;
  downlink?: number;
  rtt?: number;
  saveData?: boolean;
};

type NavigatorWithNetworkInformation = Navigator & {
  connection?: NetworkInformationLike;
  mozConnection?: NetworkInformationLike;
  webkitConnection?: NetworkInformationLike;
};

function getNetworkConnection(): NetworkInformationLike | null {
  if (typeof navigator === 'undefined') return null;
  const nav = navigator as NavigatorWithNetworkInformation;
  return nav.connection ?? nav.mozConnection ?? nav.webkitConnection ?? null;
}

export class OfflineSyncService {
  private static instance: OfflineSyncService | undefined;
  private static readonly RECONNECT_SYNC_DEBOUNCE_MS = 1_000;

  private readonly config: OfflineConfigService;
  private readonly sessionManager: OfflineSessionManager;
  private readonly orderManager: OfflineOrderManager;
  private syncInterval: ReturnType<typeof setInterval> | null = null;
  private statusPollInterval: ReturnType<typeof setInterval> | null = null;
  private reconnectSyncTimer: ReturnType<typeof setTimeout> | null = null;
  private netInfoUnsubscribe: (() => void) | null = null;
  private networkConnection: NetworkInformationLike | null = null;
  private isSyncing = false;
  private lastSyncAt: Date | null = null;
  private pendingOrdersCount = 0;
  private lastKnownOnline: boolean | null = null;
  private lastServerHealthStatus: ServerSyncHealth['status'] | null = null;

  private constructor() {
    this.config = OfflineConfigService.getInstance();
    this.sessionManager = OfflineSessionManager.getInstance();
    this.orderManager = OfflineOrderManager.getInstance({ autoSync: false });
    this.setupListeners();
    void this.initializeOnlineState();
    this.startAutoSync();
    this.startStatusPolling();
  }

  static getInstance(): OfflineSyncService {
    if (!OfflineSyncService.instance) {
      OfflineSyncService.instance = new OfflineSyncService();
    }
    return OfflineSyncService.instance;
  }

  private async initializeOnlineState(): Promise<void> {
    this.lastKnownOnline = await this.isOnline();
    await this.refreshPendingCounts();
    this.emitStatusChange();
  }

  private startAutoSync(): void {
    const intervalMs = this.config.get('SYNC_INTERVAL_SECONDS') * 1000;

    this.syncInterval = setInterval(() => {
      void this.autoSync();
    }, intervalMs);
  }

  private startStatusPolling(): void {
    const intervalMs = this.config.get('STATUS_POLL_INTERVAL_SECONDS') * 1000;

    this.statusPollInterval = setInterval(() => {
      void this.pollSyncStatus();
    }, intervalMs);
  }

  private async pollSyncStatus(): Promise<void> {
    await this.refreshPendingCounts();

    const online = await this.isOnline();
    if (this.lastKnownOnline === false && online) {
      this.scheduleReconnectSync();
    }
    this.lastKnownOnline = online;

    if (online) {
      await this.pollServerSyncHealth();
    }

    const status = this.getSyncStatus();
    this.emitStatusChange(status);
  }

  private async hasValidAccessToken(): Promise<boolean> {
    const token = await sessionManager.getAccessToken();
    if (!token) return false;
    return !sessionManager.isExpired(token);
  }

  private async pollServerSyncHealth(): Promise<void> {
    // Requires PaymentTake — never call from login / unauthenticated bootstrap.
    if (!(await this.hasValidAccessToken())) {
      return;
    }

    try {
      const path = this.config.get('SYNC_ENDPOINTS').HEALTH.replace(/^\/api/, '');
      const raw = await apiClient.get<{ data?: ServerSyncHealth; success?: boolean }>(path);
      const health = raw?.data;
      if (!health) return;

      if (health.status === 'warning' && this.lastServerHealthStatus !== 'warning') {
        eventEmitter.emit('sync:warning', {
          message: `${health.pendingOrders} Offline-Bestellungen warten auf dem Server`,
        });
      }

      this.lastServerHealthStatus = health.status;
    } catch {
      // Non-blocking — local sync continues when health endpoint is unavailable.
    }
  }

  private scheduleReconnectSync(): void {
    if (this.reconnectSyncTimer) {
      clearTimeout(this.reconnectSyncTimer);
    }

    this.reconnectSyncTimer = setTimeout(() => {
      this.reconnectSyncTimer = null;
      eventEmitter.emit('sync:online');
      void this.autoSync();
    }, OfflineSyncService.RECONNECT_SYNC_DEBOUNCE_MS);
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
      const message = error instanceof Error ? error.message : 'Offline sync failed';
      void OfflineSyncHistory.getInstance().record({
        type: 'order',
        status: 'failed',
        message,
      });

      eventEmitter.emit('sync:error', error instanceof Error ? error : new Error(message));

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

    return await this.syncAll();
  }

  private async isOnline(): Promise<boolean> {
    return await fetchIsNetworkOnline();
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

  private emitStatusChange(status: SyncStatus = this.getSyncStatus()): void {
    eventEmitter.emit('sync:status', status);
  }

  private handleConnectivityChange(online: boolean): void {
    const wasOffline = this.lastKnownOnline === false;
    this.lastKnownOnline = online;

    if (online) {
      if (wasOffline) {
        this.scheduleReconnectSync();
      }
      return;
    }

    eventEmitter.emit('sync:offline');
    this.emitStatusChange();
  }

  private readonly handleOnline = (): void => {
    void this.evaluateNetworkStatus();
  };

  private readonly handleOffline = (): void => {
    this.handleConnectivityChange(false);
  };

  private readonly handleNetworkChange = (): void => {
    void this.evaluateNetworkStatus();
  };

  private async evaluateNetworkStatus(): Promise<void> {
    const online = await this.isOnline();
    this.handleConnectivityChange(online);
  }

  /** Web: Network Information API + online/offline fallback. Native: NetInfo handles events. */
  private checkNetworkStatus(): void {
    if (typeof navigator === 'undefined' || typeof window === 'undefined') {
      return;
    }

    this.networkConnection = getNetworkConnection();
    if (this.networkConnection) {
      this.networkConnection.addEventListener('change', this.handleNetworkChange);
    }

    window.addEventListener('online', this.handleOnline);
    window.addEventListener('offline', this.handleOffline);
  }

  private setupListeners(): void {
    this.netInfoUnsubscribe = NetInfo.addEventListener((state) => {
      this.handleConnectivityChange(isNetworkOnline(state));
    });

    this.checkNetworkStatus();
  }

  private teardownNetworkStatus(): void {
    this.networkConnection?.removeEventListener('change', this.handleNetworkChange);
    this.networkConnection = null;

    if (typeof window !== 'undefined') {
      window.removeEventListener('online', this.handleOnline);
      window.removeEventListener('offline', this.handleOffline);
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

    if (this.statusPollInterval) {
      clearInterval(this.statusPollInterval);
      this.statusPollInterval = null;
    }

    if (this.reconnectSyncTimer) {
      clearTimeout(this.reconnectSyncTimer);
      this.reconnectSyncTimer = null;
    }

    this.netInfoUnsubscribe?.();
    this.netInfoUnsubscribe = null;
    this.teardownNetworkStatus();
  }

  static resetForTests(): void {
    OfflineSyncService.instance?.destroy();
    OfflineSyncService.instance = undefined;
  }
}
