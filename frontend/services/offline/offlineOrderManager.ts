import type { IOfflineStorage, OfflineOrder } from './offlineStorage';
import { getOfflineStorage } from './offlineStorage';
import { OfflineSyncHistory } from './offlineSyncHistory';
import { apiClient } from '../api/config';
import {
  paymentPayloadContainsVoucherSecrets,
  VOUCHER_OFFLINE_NOT_ALLOWED_MESSAGE_DE,
  type PendingPaymentPayload,
} from '../payment/pendingPaymentQueue';

import { OFFLINE_CONFIG } from '@/constants/offlineConfig';
import { eventEmitter } from '@/utils/eventEmitter';
import { fetchIsNetworkOnline } from '@/utils/isNetworkOnline';

const EXPIRY_MS = OFFLINE_CONFIG.OFFLINE_EXPIRY_HOURS * 60 * 60 * 1000;

/** German POS operator message when local offline queue is at capacity. */
export const OFFLINE_LIMIT_EXCEEDED_MESSAGE_DE =
  'Offline-Limit erreicht. Bitte Internetverbindung prüfen und synchronisieren.';

export type SyncDetail = {
  success: boolean;
  id: string;
  error?: string;
};

export type SyncResult = {
  success: boolean;
  message: string;
  details?: SyncDetail[];
};

export type OfflineStatus = {
  isOnline: boolean;
  pendingCount: number;
  isSyncing: boolean;
  oldestPending: string | null;
};

export type ExpiryWarningListener = (offlineOrderId: string, hoursUntilExpiry: number) => void;

export type OfflineOrderManagerOptions = {
  autoSync?: boolean;
  syncIntervalMs?: number;
  maxSyncAttempts?: number;
  onExpiryWarning?: ExpiryWarningListener;
  isOnlineChecker?: () => Promise<boolean>;
};

type OfflineOrderSaveApiResponse = {
  success?: boolean;
  data?: {
    id?: string;
    offlineOrderId?: string;
  };
};

type ReplayOfflineOrdersApiResponse = {
  success?: boolean;
  data?: {
    success?: number;
    failed?: number;
    details?: {
      orderId?: string;
      success?: boolean;
      paymentId?: string | null;
      invoiceNumber?: string | null;
      errorMessage?: string | null;
    }[];
  };
};

function newUuid(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `${Date.now()}-${Math.random().toString(36).slice(2, 12)}`;
}

function formatOfflineTimestamp(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return (
    `${date.getUTCFullYear()}${pad(date.getUTCMonth() + 1)}${pad(date.getUTCDate())}` +
    `${pad(date.getUTCHours())}${pad(date.getUTCMinutes())}${pad(date.getUTCSeconds())}`
  );
}

function randomFourDigits(): string {
  return String(Math.floor(Math.random() * 10000)).padStart(4, '0');
}

function asRecord(value: unknown): Record<string, unknown> | null {
  return value != null && typeof value === 'object' ? (value as Record<string, unknown>) : null;
}

function extractPaymentRequest(orderData: unknown): PendingPaymentPayload | null {
  const root = asRecord(orderData);
  if (!root) return null;

  const nested = asRecord(root.paymentRequest) ?? asRecord(root.PaymentRequest);
  const candidate = nested ?? root;
  if (typeof candidate.cashRegisterId !== 'string') return null;
  return candidate as unknown as PendingPaymentPayload;
}

function extractCashRegisterId(orderData: unknown): string | null {
  const paymentRequest = extractPaymentRequest(orderData);
  return paymentRequest?.cashRegisterId?.trim() || null;
}

function calculateTotal(orderData: unknown): number {
  const root = asRecord(orderData);
  if (!root) return 0;

  const paymentRequest = extractPaymentRequest(orderData);
  if (paymentRequest?.totalAmount != null && paymentRequest.totalAmount > 0) {
    return paymentRequest.totalAmount;
  }

  const direct = root.orderTotal ?? root.totalAmount ?? root.total;
  if (typeof direct === 'number' && direct > 0) return direct;

  const items = root.items;
  if (Array.isArray(items)) {
    return items.reduce((sum, row) => {
      const item = asRecord(row);
      if (!item) return sum;
      const qty = typeof item.quantity === 'number' ? item.quantity : 1;
      const price =
        typeof item.unitPrice === 'number'
          ? item.unitPrice
          : typeof item.price === 'number'
            ? item.price
            : 0;
      return sum + qty * price;
    }, 0);
  }

  return 0;
}

function calculateRetryDelayMs(attempt: number): number {
  return Math.pow(2, attempt) * 1000;
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function isNonRetryableSyncError(error: unknown): boolean {
  if (!(error instanceof Error)) return false;
  return (
    error.message === 'cash_register_id_missing' ||
    error.message === VOUCHER_OFFLINE_NOT_ALLOWED_MESSAGE_DE
  );
}

async function defaultIsOnlineChecker(): Promise<boolean> {
  return await fetchIsNetworkOnline();
}

export class OfflineOrderManager {
  static getInstance(options?: OfflineOrderManagerOptions): OfflineOrderManager {
    return getOfflineOrderManager(options);
  }

  private readonly storage: IOfflineStorage;
  private readonly syncIntervalMs: number;
  private readonly maxSyncAttempts: number;
  private readonly onExpiryWarning?: ExpiryWarningListener;
  private readonly isOnlineChecker: () => Promise<boolean>;

  private currentReplay = false;
  private intervalId: ReturnType<typeof setInterval> | null = null;

  constructor(storage: IOfflineStorage, options: OfflineOrderManagerOptions = {}) {
    this.storage = storage;
    this.syncIntervalMs = options.syncIntervalMs ?? 30_000;
    this.maxSyncAttempts = options.maxSyncAttempts ?? 3;
    this.onExpiryWarning = options.onExpiryWarning;
    this.isOnlineChecker = options.isOnlineChecker ?? defaultIsOnlineChecker;

    if (options.autoSync !== false) {
      this.startAutoSync();
    }
  }

  /** Persist a full offline order locally (no backend call until sync). */
  async saveOrder(orderData: unknown, paymentMethod: string): Promise<OfflineOrder> {
    const paymentRequest = extractPaymentRequest(orderData);
    if (paymentPayloadContainsVoucherSecrets(paymentRequest?.payment)) {
      throw new Error(VOUCHER_OFFLINE_NOT_ALLOWED_MESSAGE_DE);
    }

    const maxLimit = OFFLINE_CONFIG.MAX_OFFLINE_TRANSACTIONS;
    const pendingBefore = await this.storage.getPendingOrders();
    if (pendingBefore.length >= maxLimit) {
      eventEmitter.emit('offline:limit-exceeded', {
        pendingCount: pendingBefore.length,
        maxLimit,
      });
      throw new Error(OFFLINE_LIMIT_EXCEEDED_MESSAGE_DE);
    }

    const now = new Date();
    const order: OfflineOrder = {
      id: newUuid(),
      offlineOrderId: `OFFLINE-${formatOfflineTimestamp(now)}-${randomFourDigits()}`,
      orderData,
      orderTotal: calculateTotal(orderData),
      paymentMethod: paymentMethod.trim(),
      createdAt: now.toISOString(),
      expiresAt: new Date(now.getTime() + EXPIRY_MS).toISOString(),
      status: 'pending',
      syncAttempts: 0,
      serverOrderGuid: null,
      lastError: null,
    };

    await this.storage.saveOrder(order);
    await this.checkExpiryWarning(order);

    const pending = await this.storage.getPendingOrders();
    const remaining = Math.max(0, maxLimit - pending.length);
    eventEmitter.emit('offline:order-saved', {
      offlineOrderId: order.offlineOrderId,
      pendingCount: pending.length,
      maxLimit,
      remaining,
    });

    return order;
  }

  private startAutoSync(): void {
    if (this.intervalId != null) return;

    this.intervalId = setInterval(() => {
      void this.runAutoSyncTick();
    }, this.syncIntervalMs);
  }

  private async runAutoSyncTick(): Promise<void> {
    if (!(await this.isOnline()) || this.currentReplay) return;
    try {
      await this.syncPendingOrders();
    } catch (err) {
      console.warn('[OfflineOrderManager] Auto-sync failed:', err);
    }
  }

  /** Stop background auto-sync (tests / teardown). */
  destroy(): void {
    if (this.intervalId != null) {
      clearInterval(this.intervalId);
      this.intervalId = null;
    }
  }

  async syncPendingOrders(): Promise<SyncResult> {
    if (this.currentReplay) {
      return { success: false, message: 'Sync in progress' };
    }
    if (!(await this.isOnline())) {
      return { success: false, message: 'Offline' };
    }

    await this.purgeExpiredPending();

    const pending = await this.storage.getPendingOrders();
    if (pending.length === 0) {
      return { success: true, message: 'No pending orders' };
    }

    this.currentReplay = true;
    try {
      return await this.sendOrdersToBackend(pending);
    } finally {
      this.currentReplay = false;
    }
  }

  private async sendOrdersToBackend(orders: OfflineOrder[]): Promise<SyncResult> {
    const details: SyncDetail[] = [];
    const uploadedById = new Map<string, OfflineOrder>();
    const total = orders.length;

    this.emitSyncProgress(0, total);

    if (total > 0) {
      void OfflineSyncHistory.getInstance().record({
        type: 'order',
        status: 'pending',
        message: `Sync started for ${total} offline order(s)`,
      });
    }

    for (const order of orders) {
      const cashRegisterId = extractCashRegisterId(order.orderData);
      if (!cashRegisterId) {
        await this.markAttemptFailed(order, 'cash_register_id_missing');
        this.pushSyncDetail(
          details,
          {
            success: false,
            id: order.id,
            error: 'cash_register_id_missing',
          },
          total,
          order
        );
        continue;
      }

      try {
        const working = order.serverOrderGuid
          ? order
          : await this.syncWithRetry(order, cashRegisterId);
        uploadedById.set(working.id, working);
      } catch (err) {
        const message = err instanceof Error ? err.message : 'upload_failed';
        await this.markAttemptFailed(order, message);
        this.pushSyncDetail(
          details,
          { success: false, id: order.id, error: message },
          total,
          order
        );
      }
    }

    const byRegister = new Map<string, OfflineOrder[]>();
    for (const order of uploadedById.values()) {
      const registerId = extractCashRegisterId(order.orderData);
      if (!registerId) continue;
      const bucket = byRegister.get(registerId) ?? [];
      bucket.push(order);
      byRegister.set(registerId, bucket);
    }

    for (const [cashRegisterId, registerOrders] of byRegister) {
      try {
        const replayDetails = await this.replayRegisterWithRetry(cashRegisterId);
        for (const order of registerOrders) {
          const match = replayDetails.find((d) => d.orderId === order.serverOrderGuid);
          if (match?.success) {
            await this.storage.deleteOrder(order.id);
            this.pushSyncDetail(details, { success: true, id: order.id }, total, order);
          } else {
            const error = match?.errorMessage ?? 'offline_order_replay_failed';
            await this.markAttemptFailed(order, error);
            this.pushSyncDetail(details, { success: false, id: order.id, error }, total, order);
          }
        }
      } catch (err) {
        const message = err instanceof Error ? err.message : 'replay_failed';
        for (const order of registerOrders) {
          await this.markAttemptFailed(order, message);
          this.pushSyncDetail(
            details,
            { success: false, id: order.id, error: message },
            total,
            order
          );
        }
      }
    }

    const failed = details.filter((d) => !d.success).length;
    return {
      success: failed === 0,
      message: failed === 0 ? 'Sync completed' : `${failed} order(s) failed`,
      details,
    };
  }

  private emitSyncProgress(current: number, total: number): void {
    eventEmitter.emit('sync:progress', { current, total });
  }

  private pushSyncDetail(
    details: SyncDetail[],
    detail: SyncDetail,
    total: number,
    order?: OfflineOrder
  ): void {
    details.push(detail);
    this.emitSyncProgress(details.length, total);
    this.recordSyncHistory(detail, order);
  }

  private recordSyncHistory(detail: SyncDetail, order?: OfflineOrder): void {
    const label = order?.offlineOrderId ?? detail.id;
    const message = detail.success
      ? `Order ${label} synced successfully`
      : (detail.error ?? `Order ${label} sync failed`);

    void OfflineSyncHistory.getInstance().recordOrderSync(
      detail.id,
      detail.success ? 'success' : 'failed',
      message
    );
  }

  /** Upload a single offline order snapshot (no replay). */
  private async syncOrder(order: OfflineOrder, cashRegisterId: string): Promise<OfflineOrder> {
    if (order.serverOrderGuid) {
      return order;
    }
    return await this.uploadOrderToBackend(order, cashRegisterId);
  }

  /** Retry upload with exponential backoff (2s, 4s, 8s). */
  private async syncWithRetry(
    order: OfflineOrder,
    cashRegisterId: string,
    attempt = 1
  ): Promise<OfflineOrder> {
    try {
      return await this.syncOrder(order, cashRegisterId);
    } catch (error) {
      if (attempt < this.maxSyncAttempts && !isNonRetryableSyncError(error)) {
        await sleep(calculateRetryDelayMs(attempt));
        return await this.syncWithRetry(order, cashRegisterId, attempt + 1);
      }
      throw error;
    }
  }

  private async replayRegisterWithRetry(
    cashRegisterId: string,
    attempt = 1
  ): Promise<
    {
      orderId?: string;
      success?: boolean;
      errorMessage?: string | null;
    }[]
  > {
    try {
      return await this.replayRegister(cashRegisterId);
    } catch (error) {
      if (attempt < this.maxSyncAttempts) {
        await sleep(calculateRetryDelayMs(attempt));
        return await this.replayRegisterWithRetry(cashRegisterId, attempt + 1);
      }
      throw error;
    }
  }

  private async uploadOrderToBackend(
    order: OfflineOrder,
    cashRegisterId: string
  ): Promise<OfflineOrder> {
    const body = {
      cashRegisterId,
      orderData: order.orderData,
      orderTotal: order.orderTotal,
      paymentMethod: order.paymentMethod,
    };

    const raw = await apiClient.post<OfflineOrderSaveApiResponse>('/pos/offline-orders', body);
    const data = raw?.data ?? raw?.data;
    if (!data?.id) {
      throw new Error('offline_order_save_incomplete');
    }

    const updated: OfflineOrder = {
      ...order,
      serverOrderGuid: data.id,
      offlineOrderId: data.offlineOrderId ?? order.offlineOrderId,
      lastError: null,
    };
    await this.storage.saveOrder(updated);
    return updated;
  }

  private async replayRegister(cashRegisterId: string): Promise<
    {
      orderId?: string;
      success?: boolean;
      errorMessage?: string | null;
    }[]
  > {
    const raw = await apiClient.post<ReplayOfflineOrdersApiResponse>(
      `/pos/offline-orders/replay?cashRegisterId=${encodeURIComponent(cashRegisterId)}`,
      {}
    );
    return raw?.data?.details ?? [];
  }

  private async markAttemptFailed(order: OfflineOrder, error: string): Promise<void> {
    const attempts = (order.syncAttempts ?? 0) + 1;
    const failed = attempts >= this.maxSyncAttempts;
    const updated: OfflineOrder = {
      ...order,
      syncAttempts: attempts,
      lastError: error,
      status: failed ? 'failed' : 'pending',
    };
    await this.storage.saveOrder(updated);
  }

  private async purgeExpiredPending(): Promise<void> {
    const pending = await this.storage.getPendingOrders();
    const now = Date.now();
    await Promise.all(
      pending
        .filter((o) => new Date(o.expiresAt).getTime() <= now)
        .map((o) => this.storage.deleteOrder(o.id))
    );
  }

  private async checkExpiryWarning(order: OfflineOrder): Promise<void> {
    const hoursUntilExpiry = (new Date(order.expiresAt).getTime() - Date.now()) / (1000 * 60 * 60);
    if (hoursUntilExpiry <= 24 && hoursUntilExpiry > 0) {
      this.showExpiryWarning(order.offlineOrderId, hoursUntilExpiry);
    }
  }

  private showExpiryWarning(offlineOrderId: string, hoursUntilExpiry: number): void {
    if (this.onExpiryWarning) {
      this.onExpiryWarning(offlineOrderId, hoursUntilExpiry);
      return;
    }
    console.warn(
      `[OfflineOrderManager] Order ${offlineOrderId} expires in ${Math.ceil(hoursUntilExpiry)} hour(s).`
    );
  }

  async isOnline(): Promise<boolean> {
    return await this.isOnlineChecker();
  }

  async getStatus(): Promise<OfflineStatus> {
    const pending = await this.storage.getPendingOrders();
    return {
      isOnline: await this.isOnline(),
      pendingCount: pending.length,
      isSyncing: this.currentReplay,
      oldestPending: pending.length > 0 ? pending[0].createdAt : null,
    };
  }

  /** Pending offline order count (local queue). */
  async getPendingCount(): Promise<number> {
    const pending = await this.storage.getPendingOrders();
    return pending.length;
  }

  /**
   * Hours until the soonest pending order expires.
   * Returns the full offline window when the queue is empty.
   */
  async getHoursRemaining(
    fallbackHours: number = OFFLINE_CONFIG.OFFLINE_EXPIRY_HOURS
  ): Promise<number> {
    const pending = await this.storage.getPendingOrders();
    if (pending.length === 0) {
      return fallbackHours;
    }

    const now = Date.now();
    let minHours = Number.POSITIVE_INFINITY;
    for (const order of pending) {
      const hours = (new Date(order.expiresAt).getTime() - now) / (1000 * 60 * 60);
      if (hours < minHours) {
        minHours = hours;
      }
    }

    if (!Number.isFinite(minHours)) {
      return fallbackHours;
    }

    return Math.max(0, Math.ceil(minHours));
  }

  async getPendingOrders(): Promise<OfflineOrder[]> {
    return await this.storage.getPendingOrders();
  }

  async getOrder(id: string): Promise<OfflineOrder | null> {
    return await this.storage.getOrder(id);
  }
}

let sharedManager: OfflineOrderManager | null = null;

export function getOfflineOrderManager(options?: OfflineOrderManagerOptions): OfflineOrderManager {
  if (!sharedManager) {
    sharedManager = new OfflineOrderManager(getOfflineStorage(), options);
  }
  return sharedManager;
}

export function resetOfflineOrderManagerForTests(): void {
  sharedManager?.destroy();
  sharedManager = null;
}
