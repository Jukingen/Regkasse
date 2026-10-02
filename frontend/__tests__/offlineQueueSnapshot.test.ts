import { describe, expect, it } from '@jest/globals';

import {
  isNonFiscalPaymentEntry,
  mapPaymentQueueItem,
  mergeOfflineQueueItems,
} from '../services/offline/offlineQueueSnapshot';
import type { OfflineOrder } from '../services/offline/offlineStorage';
import type { PendingPaymentEntry } from '../services/payment/pendingPaymentQueue';

function payment(overrides: Partial<PendingPaymentEntry> = {}): PendingPaymentEntry {
  return {
    queueId: 'tx-1',
    createdAt: '2026-09-30T10:00:00.000Z',
    cashRegisterId: 'reg-1',
    paymentRequest: {
      customerId: 'c1',
      items: [{ productId: 'p1', quantity: 1, taxType: 'Standard' }],
      payment: { method: 'Cash', tseRequired: true },
      tableNumber: 0,
      totalAmount: 9.9,
      cashRegisterId: 'reg-1',
    },
    status: 'Pending',
    isSynced: false,
    ...overrides,
  };
}

describe('offlineQueueSnapshot', () => {
  it('forbids delete on fiscal TSE payment intents', () => {
    const fiscal = mapPaymentQueueItem(payment());
    expect(fiscal.canDelete).toBe(false);
    expect(isNonFiscalPaymentEntry(payment())).toBe(false);
  });

  it('allows delete only when tseRequired is false', () => {
    const nonFiscal = mapPaymentQueueItem(
      payment({
        queueId: 'tx-open',
        paymentRequest: {
          customerId: 'c1',
          items: [{ productId: 'p1', quantity: 1, taxType: 'Standard' }],
          payment: { method: 'Cash', tseRequired: false },
          tableNumber: 0,
          totalAmount: 3,
          cashRegisterId: 'reg-1',
        },
        status: 'Failed',
      })
    );
    expect(nonFiscal.canDelete).toBe(true);
  });

  it('merges order snapshots and payment intents', () => {
    const orders: OfflineOrder[] = [
      {
        id: 'ord-1',
        offlineOrderId: 'off-1',
        orderData: {},
        orderTotal: 20,
        paymentMethod: 'Cash',
        createdAt: '2026-09-30T09:00:00.000Z',
        expiresAt: '2026-10-03T09:00:00.000Z',
        status: 'pending',
      },
    ];
    const merged = mergeOfflineQueueItems(orders, [payment({ queueId: 'tx-2' })]);
    expect(merged.map((row) => row.id)).toEqual(['tx-2', 'ord-1']);
    expect(merged.find((row) => row.source === 'order')?.canDelete).toBe(false);
  });
});
