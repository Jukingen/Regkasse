import { beforeEach, describe, expect, it, jest } from '@jest/globals';

import {
  AsyncStorageAdapter,
  createOfflineStorage,
  type OfflineOrder,
} from '../services/offline/offlineStorage';

jest.mock('react-native', () => ({
  Platform: { OS: 'android' },
}));

/**
 * AsyncStorageAdapter persists via utils/storage (not AsyncStorage directly).
 * In-memory mock keeps the suite isolated from Platform / AsyncStorage init order.
 */
const mockMemory = new Map<string, string>();

jest.mock('../utils/storage', () => ({
  storage: {
    getItem: jest.fn(async (key: string) => mockMemory.get(key) ?? null),
    setItem: jest.fn(async (key: string, value: string) => {
      mockMemory.set(key, value);
    }),
    removeItem: jest.fn(async (key: string) => {
      mockMemory.delete(key);
    }),
    clear: jest.fn(async () => {
      mockMemory.clear();
    }),
    getJson: jest.fn(async (key: string) => {
      const raw = mockMemory.get(key);
      if (raw == null) return null;
      return JSON.parse(raw) as unknown;
    }),
    setJson: jest.fn(async (key: string, value: unknown) => {
      mockMemory.set(key, JSON.stringify(value));
    }),
  },
}));

function sampleOrder(overrides: Partial<OfflineOrder> = {}): OfflineOrder {
  return {
    id: 'local-1',
    offlineOrderId: 'off-1',
    orderData: { items: [{ productId: 'p1', quantity: 1 }] },
    orderTotal: 12.5,
    paymentMethod: 'Cash',
    createdAt: '2026-07-21T10:00:00.000Z',
    expiresAt: '2026-07-24T10:00:00.000Z',
    status: 'pending',
    ...overrides,
  };
}

describe('AsyncStorageAdapter offline queue persistence', () => {
  beforeEach(() => {
    mockMemory.clear();
  });

  it('writes and reads pending offline orders', async () => {
    const offlineStorage = new AsyncStorageAdapter();
    const order = sampleOrder();

    await offlineStorage.saveOrder(order);

    const pending = await offlineStorage.getPendingOrders();
    expect(pending).toHaveLength(1);
    expect(pending[0]).toMatchObject({
      id: 'local-1',
      offlineOrderId: 'off-1',
      orderTotal: 12.5,
      status: 'pending',
    });
    expect(pending[0].orderData).toEqual(order.orderData);
  });

  it('updates status and filters pending vs synced', async () => {
    const offlineStorage = new AsyncStorageAdapter();
    await offlineStorage.saveOrder(sampleOrder({ id: 'a', status: 'pending' }));
    await offlineStorage.saveOrder(
      sampleOrder({
        id: 'b',
        offlineOrderId: 'off-2',
        createdAt: '2026-07-21T11:00:00.000Z',
        status: 'pending',
      })
    );

    await offlineStorage.updateOrderStatus('a', 'synced');

    const pending = await offlineStorage.getPendingOrders();
    expect(pending.map((o) => o.id)).toEqual(['b']);

    await offlineStorage.deleteAllSynced();
    expect(await offlineStorage.getOrder('a')).toBeNull();
    expect(await offlineStorage.getOrder('b')).not.toBeNull();
  });

  it('upserts by id without duplicating rows', async () => {
    const offlineStorage = new AsyncStorageAdapter();
    await offlineStorage.saveOrder(sampleOrder({ orderTotal: 10 }));
    await offlineStorage.saveOrder(sampleOrder({ orderTotal: 22 }));

    const pending = await offlineStorage.getPendingOrders();
    expect(pending).toHaveLength(1);
    expect(pending[0].orderTotal).toBe(22);
  });

  it('selects AsyncStorage adapter for native platforms', () => {
    expect(createOfflineStorage()).toBeInstanceOf(AsyncStorageAdapter);
  });
});
