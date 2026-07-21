/**
 * Offline order queue — full order snapshots (items, modifiers, totals) for POS reconnect replay.
 * Complements pendingPaymentQueue (payment-intent only). Invariant: no voucher plaintext in local storage.
 */
import {
  paymentPayloadContainsVoucherSecrets,
  VOUCHER_OFFLINE_NOT_ALLOWED_MESSAGE_DE,
  type PendingPaymentPayload,
} from './pendingPaymentQueue';
import { storage } from '../../utils/storage';
import { apiClient } from '../api/config';

const STORAGE_KEY = '@regkasse/offline_orders_v1';

export type OfflineOrderStatus = 'pending' | 'synced' | 'failed' | 'uploaded';

/** Full offline order snapshot sent to backend order_data JSONB. */
export type OfflineOrderSnapshot = {
  paymentRequest: PendingPaymentPayload;
  items?: {
    productId: string;
    productName?: string;
    quantity: number;
    unitPrice?: number;
    taxType?: string;
    modifiers?: { modifierId?: string; name?: string; price?: number }[];
  }[];
  tableNumber?: number;
  notes?: string;
};

export type OfflineOrderQueueEntry = {
  /** Local queue id (UUID). */
  localId: string;
  createdAt: string;
  cashRegisterId: string;
  paymentMethod: string;
  orderTotal: number;
  orderData: OfflineOrderSnapshot;
  status: OfflineOrderStatus;
  /** Server-assigned offline_order_id after POST save. */
  serverOfflineOrderId?: string | null;
  serverOrderGuid?: string | null;
  syncedPaymentId?: string | null;
  syncedInvoiceNumber?: string | null;
  lastAttemptAt?: string;
  lastError?: string;
  syncAttempts?: number;
};

export type OfflineOrderSaveResponse = {
  success?: boolean;
  data?: {
    id?: string;
    offlineOrderId?: string;
    status?: string;
    expiresAtUtc?: string;
    hoursRemaining?: number;
  };
};

export type ReplayOfflineOrderApiResult = {
  orderId?: string;
  success?: boolean;
  paymentId?: string | null;
  invoiceNumber?: string | null;
  errorMessage?: string | null;
};

export type ReplayOfflineOrdersApiResponse = {
  success?: boolean;
  data?: {
    total?: number;
    success?: number;
    failed?: number;
    details?: ReplayOfflineOrderApiResult[];
  };
};

async function readQueue(): Promise<OfflineOrderQueueEntry[]> {
  const raw = await storage.getItem(STORAGE_KEY);
  if (!raw) return [];
  try {
    const parsed = JSON.parse(raw) as unknown;
    return Array.isArray(parsed) ? (parsed as OfflineOrderQueueEntry[]) : [];
  } catch {
    return [];
  }
}

async function writeQueue(entries: OfflineOrderQueueEntry[]): Promise<void> {
  await storage.setItem(STORAGE_KEY, JSON.stringify(entries));
}

function newLocalId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `${Date.now()}-${Math.random().toString(36).slice(2, 12)}`;
}

function normalizeEntry(entry: OfflineOrderQueueEntry): OfflineOrderQueueEntry {
  return {
    ...entry,
    status: entry.status ?? 'pending',
    serverOfflineOrderId: entry.serverOfflineOrderId ?? null,
    serverOrderGuid: entry.serverOrderGuid ?? null,
    syncedPaymentId: entry.syncedPaymentId ?? null,
    syncedInvoiceNumber: entry.syncedInvoiceNumber ?? null,
    syncAttempts: entry.syncAttempts ?? 0,
  };
}

/**
 * Append a full offline order to the local queue (or return existing local id for same idempotency key).
 */
export async function enqueueOfflineOrder(input: {
  cashRegisterId: string;
  paymentMethod: string;
  orderTotal: number;
  orderData: OfflineOrderSnapshot;
}): Promise<string> {
  if (paymentPayloadContainsVoucherSecrets(input.orderData.paymentRequest?.payment)) {
    throw new Error(VOUCHER_OFFLINE_NOT_ALLOWED_MESSAGE_DE);
  }

  const queue = (await readQueue()).map(normalizeEntry);
  const idem = input.orderData.paymentRequest?.idempotencyKey?.trim();
  if (idem) {
    const existing = queue.find(
      (e) =>
        (e.status === 'pending' || e.status === 'uploaded') &&
        e.orderData.paymentRequest?.idempotencyKey === idem
    );
    if (existing) return existing.localId;
  }

  const entry: OfflineOrderQueueEntry = {
    localId: newLocalId(),
    createdAt: new Date().toISOString(),
    cashRegisterId: input.cashRegisterId,
    paymentMethod: input.paymentMethod,
    orderTotal: input.orderTotal,
    orderData: structuredCloneSafe(input.orderData),
    status: 'pending',
    syncAttempts: 0,
  };

  queue.push(entry);
  await writeQueue(queue);
  return entry.localId;
}

function structuredCloneSafe<T>(value: T): T {
  try {
    return structuredClone(value);
  } catch {
    return JSON.parse(JSON.stringify(value)) as T;
  }
}

export async function getOfflineOrderQueue(): Promise<OfflineOrderQueueEntry[]> {
  const queue = (await readQueue()).map(normalizeEntry);
  return queue
    .filter((e) => e.status === 'pending' || e.status === 'uploaded')
    .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
}

export async function getAllOfflineOrderEntries(): Promise<OfflineOrderQueueEntry[]> {
  const queue = (await readQueue()).map(normalizeEntry);
  return queue.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
}

export async function removeOfflineOrderByLocalId(localId: string): Promise<void> {
  const queue = (await readQueue()).map(normalizeEntry);
  await writeQueue(queue.filter((e) => e.localId !== localId));
}

async function uploadEntryToServer(entry: OfflineOrderQueueEntry): Promise<OfflineOrderQueueEntry> {
  if (entry.serverOfflineOrderId) return entry;

  const body = {
    cashRegisterId: entry.cashRegisterId,
    orderData: entry.orderData,
    orderTotal: entry.orderTotal,
    paymentMethod: entry.paymentMethod,
  };

  const raw = await apiClient.post<OfflineOrderSaveResponse>('/pos/offline-orders', body);
  const data = raw?.data ?? raw?.data;
  if (!data?.offlineOrderId) {
    throw new Error('offline_order_save_incomplete');
  }

  return {
    ...entry,
    status: 'uploaded',
    serverOfflineOrderId: data.offlineOrderId,
    serverOrderGuid: data.id ?? null,
    lastError: undefined,
  };
}

/**
 * Upload local pending orders to backend, then replay pending orders per register.
 */
export async function syncOfflineOrderQueue(): Promise<{
  uploaded: number;
  replayed: number;
  failed: number;
}> {
  const pending = await getOfflineOrderQueue();
  if (pending.length === 0) return { uploaded: 0, replayed: 0, failed: 0 };

  let uploaded = 0;
  let replayed = 0;
  let failed = 0;

  const queue = (await readQueue()).map(normalizeEntry);

  for (const entry of pending) {
    const idx = queue.findIndex((e) => e.localId === entry.localId);
    if (idx < 0) continue;

    try {
      queue[idx].lastAttemptAt = new Date().toISOString();
      queue[idx].syncAttempts = (queue[idx].syncAttempts ?? 0) + 1;
      const updated = await uploadEntryToServer(queue[idx]);
      queue[idx] = updated;
      uploaded++;
    } catch (err) {
      queue[idx].lastError = err instanceof Error ? err.message : 'offline_order_upload_failed';
      failed++;
    }
  }

  await writeQueue(queue);

  const registerIds = [...new Set(pending.map((e) => e.cashRegisterId).filter(Boolean))];
  for (const cashRegisterId of registerIds) {
    try {
      const raw = await apiClient.post<ReplayOfflineOrdersApiResponse>(
        `/pos/offline-orders/replay?cashRegisterId=${encodeURIComponent(cashRegisterId)}`,
        {}
      );
      const details = raw?.data?.details ?? [];
      replayed += raw?.data?.success ?? 0;
      failed += raw?.data?.failed ?? 0;

      const refreshed = (await readQueue()).map(normalizeEntry);
      for (const detail of details ?? []) {
        if (!detail.orderId) continue;
        const match = refreshed.find((e) => e.serverOrderGuid === detail.orderId);
        if (!match) continue;
        if (detail.success) {
          match.status = 'synced';
          match.syncedPaymentId = detail.paymentId ?? null;
          match.syncedInvoiceNumber = detail.invoiceNumber ?? null;
          match.lastError = undefined;
        } else {
          match.lastError = detail.errorMessage ?? 'offline_order_replay_failed';
          if ((match.syncAttempts ?? 0) >= 3) match.status = 'failed';
        }
      }
      await writeQueue(refreshed);
    } catch (err) {
      failed += pending.filter((e) => e.cashRegisterId === cashRegisterId).length;
      console.warn('[offlineOrderQueue] Replay failed for register', cashRegisterId, err);
    }
  }

  return { uploaded, replayed, failed };
}

export async function retrySingleOfflineOrder(localId: string): Promise<{
  uploaded: number;
  replayed: number;
  failed: number;
}> {
  const queue = (await readQueue()).map(normalizeEntry);
  const entry = queue.find((e) => e.localId === localId);
  if (
    !entry ||
    (entry.status !== 'pending' && entry.status !== 'uploaded' && entry.status !== 'failed')
  ) {
    return { uploaded: 0, replayed: 0, failed: 0 };
  }

  if (entry.status === 'failed') {
    entry.status = 'uploaded';
  }

  try {
    entry.lastAttemptAt = new Date().toISOString();
    entry.syncAttempts = (entry.syncAttempts ?? 0) + 1;
    const updated = await uploadEntryToServer(entry);
    Object.assign(entry, updated);

    const raw = await apiClient.post<ReplayOfflineOrdersApiResponse>(
      `/pos/offline-orders/replay?cashRegisterId=${encodeURIComponent(entry.cashRegisterId)}`,
      {}
    );
    const detail = raw?.data?.details?.find((d) => d.orderId === entry.serverOrderGuid);
    if (detail?.success) {
      entry.status = 'synced';
      entry.syncedPaymentId = detail.paymentId ?? null;
      entry.syncedInvoiceNumber = detail.invoiceNumber ?? null;
      entry.lastError = undefined;
      await writeQueue(queue);
      return { uploaded: 1, replayed: 1, failed: 0 };
    }

    entry.lastError = detail?.errorMessage ?? 'offline_order_replay_failed';
    if ((entry.syncAttempts ?? 0) >= 3) entry.status = 'failed';
    await writeQueue(queue);
    return { uploaded: 1, replayed: 0, failed: 1 };
  } catch (err) {
    entry.lastError = err instanceof Error ? err.message : 'offline_order_retry_failed';
    await writeQueue(queue);
    return { uploaded: 0, replayed: 0, failed: 1 };
  }
}

export async function getOfflineOrderQueueCount(): Promise<number> {
  return (await getOfflineOrderQueue()).length;
}
