import { beforeEach, describe, expect, it, jest } from '@jest/globals';

const mockStorageStore = new Map<string, string>();

jest.mock('../utils/storage', () => ({
  storage: {
    getItem: jest.fn(async (key: string) => mockStorageStore.get(key) ?? null),
    setItem: jest.fn(async (key: string, value: string) => {
      mockStorageStore.set(key, value);
    }),
    removeItem: jest.fn(async (key: string) => {
      mockStorageStore.delete(key);
    }),
  },
}));

jest.mock('../services/api/config', () => ({
  apiClient: {
    post: jest.fn(),
  },
}));

function loadOfflineOrderQueue() {
  return require('../services/payment/offlineOrderQueue') as typeof import('../services/payment/offlineOrderQueue');
}

describe('offlineOrderQueue local persistence', () => {
  beforeEach(() => {
    mockStorageStore.clear();
    jest.resetModules();
  });

  it('enqueues and reloads pending offline orders from storage', async () => {
    const { enqueueOfflineOrder, getOfflineOrderQueue, getOfflineOrderQueueCount } =
      loadOfflineOrderQueue();

    const localId = await enqueueOfflineOrder({
      cashRegisterId: 'cr-1',
      paymentMethod: 'Cash',
      orderTotal: 9.9,
      orderData: {
        paymentRequest: {
          customerId: 'c1',
          items: [{ productId: 'p1', quantity: 1, taxType: 'Standard' }],
          payment: { method: 'Cash', tseRequired: true },
          tableNumber: 1,
          totalAmount: 9.9,
          cashRegisterId: 'cr-1',
          idempotencyKey: 'idem-1',
        },
        items: [{ productId: 'p1', quantity: 1 }],
      },
    });

    expect(localId).toBeTruthy();
    expect(await getOfflineOrderQueueCount()).toBe(1);

    const queue = await getOfflineOrderQueue();
    expect(queue).toHaveLength(1);
    expect(queue[0]).toMatchObject({
      localId,
      cashRegisterId: 'cr-1',
      paymentMethod: 'Cash',
      orderTotal: 9.9,
      status: 'pending',
    });
    expect(queue[0].orderData.paymentRequest.idempotencyKey).toBe('idem-1');
  });

  it('rejects voucher plaintext from the offline queue', async () => {
    const { enqueueOfflineOrder } = loadOfflineOrderQueue();

    await expect(
      enqueueOfflineOrder({
        cashRegisterId: 'cr-1',
        paymentMethod: 'Voucher',
        orderTotal: 5,
        orderData: {
          paymentRequest: {
            customerId: 'c1',
            items: [{ productId: 'p1', quantity: 1, taxType: 'Standard' }],
            payment: { method: 'Voucher', tseRequired: false, voucherCode: 'SECRET' },
            tableNumber: 1,
            totalAmount: 5,
            cashRegisterId: 'cr-1',
          },
        },
      })
    ).rejects.toThrow(/Gutschein/);

    expect(mockStorageStore.size).toBe(0);
  });

  it('deduplicates by payment idempotency key', async () => {
    const { enqueueOfflineOrder, getOfflineOrderQueueCount } = loadOfflineOrderQueue();

    const first = await enqueueOfflineOrder({
      cashRegisterId: 'cr-1',
      paymentMethod: 'Cash',
      orderTotal: 1,
      orderData: {
        paymentRequest: {
          customerId: 'c1',
          items: [{ productId: 'p1', quantity: 1, taxType: 'Standard' }],
          payment: { method: 'Cash', tseRequired: true },
          tableNumber: 1,
          totalAmount: 1,
          cashRegisterId: 'cr-1',
          idempotencyKey: 'same-key',
        },
      },
    });
    const second = await enqueueOfflineOrder({
      cashRegisterId: 'cr-1',
      paymentMethod: 'Cash',
      orderTotal: 1,
      orderData: {
        paymentRequest: {
          customerId: 'c1',
          items: [{ productId: 'p1', quantity: 1, taxType: 'Standard' }],
          payment: { method: 'Cash', tseRequired: true },
          tableNumber: 1,
          totalAmount: 1,
          cashRegisterId: 'cr-1',
          idempotencyKey: 'same-key',
        },
      },
    });

    expect(second).toBe(first);
    expect(await getOfflineOrderQueueCount()).toBe(1);
  });
});
