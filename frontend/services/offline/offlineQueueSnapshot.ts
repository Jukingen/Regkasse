import type { OfflineOrder } from './offlineStorage';

import type { PendingPaymentEntry } from '../payment/pendingPaymentQueue';

export type OfflineQueueSource = 'order' | 'transaction';

export type OfflineQueueItemStatus = 'pending' | 'synced' | 'failed' | 'unknown';

export type OfflineQueueListItem = {
  id: string;
  source: OfflineQueueSource;
  timestamp: string;
  amount: number;
  status: OfflineQueueItemStatus;
  canRetry: boolean;
  /** Only non-fiscal local payment intents (`tseRequired === false`). */
  canDelete: boolean;
  lastError?: string | null;
};

function mapPaymentStatus(status: PendingPaymentEntry['status']): OfflineQueueItemStatus {
  switch (status) {
    case 'Synced':
      return 'synced';
    case 'Failed':
      return 'failed';
    case 'Unknown':
      return 'unknown';
    default:
      return 'pending';
  }
}

export function isNonFiscalPaymentEntry(entry: PendingPaymentEntry): boolean {
  return entry.paymentRequest?.payment?.tseRequired === false;
}

export function mapPaymentQueueItem(entry: PendingPaymentEntry): OfflineQueueListItem {
  const status = mapPaymentStatus(entry.status);
  const retryable = status === 'pending' || status === 'failed' || status === 'unknown';
  return {
    id: entry.queueId,
    source: 'transaction',
    timestamp: entry.createdAt,
    amount: Number(entry.paymentRequest?.totalAmount ?? 0),
    status,
    canRetry: retryable,
    canDelete: retryable && isNonFiscalPaymentEntry(entry),
    lastError: entry.lastError ?? null,
  };
}

export function mapOrderQueueItem(order: OfflineOrder): OfflineQueueListItem {
  const status: OfflineQueueItemStatus =
    order.status === 'synced' ? 'synced' : order.status === 'failed' ? 'failed' : 'pending';
  return {
    id: order.id,
    source: 'order',
    timestamp: order.createdAt,
    amount: Number(order.orderTotal ?? 0),
    status,
    canRetry: status === 'pending' || status === 'failed',
    canDelete: false,
    lastError: order.lastError ?? null,
  };
}

export function mergeOfflineQueueItems(
  orders: OfflineOrder[],
  payments: PendingPaymentEntry[]
): OfflineQueueListItem[] {
  const rows = [...orders.map(mapOrderQueueItem), ...payments.map(mapPaymentQueueItem)];
  return rows.sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime());
}

export function countOfflineQueueByStatus(items: OfflineQueueListItem[]): {
  pendingCount: number;
  failedCount: number;
} {
  let pendingCount = 0;
  let failedCount = 0;
  for (const item of items) {
    if (item.status === 'failed' || item.status === 'unknown') {
      failedCount += 1;
    } else if (item.status === 'pending') {
      pendingCount += 1;
    }
  }
  return { pendingCount, failedCount };
}
