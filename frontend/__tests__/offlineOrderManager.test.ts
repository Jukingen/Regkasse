import { beforeEach, describe, expect, it, jest } from '@jest/globals';

import {
  OfflineOrderManager,
  resetOfflineOrderManagerForTests,
} from '../services/offline/offlineOrderManager';
import { AsyncStorageAdapter } from '../services/offline/offlineStorage';

jest.mock('react-native', () => ({
  Platform: { OS: 'android' },
}));

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

jest.mock('../services/api/config', () => ({
  apiClient: { post: jest.fn() },
}));

jest.mock('../utils/isNetworkOnline', () => ({
  fetchIsNetworkOnline: jest.fn(async () => false),
}));

function cashSnapshot(overrides: Record<string, unknown> = {}) {
  return {
    paymentRequest: {
      customerId: 'c1',
      items: [{ productId: 'p1', quantity: 1, taxType: 'standard' }],
      payment: { method: 'cash', tseRequired: true },
      tableNumber: 1,
      totalAmount: 9.9,
      cashRegisterId: 'cr-1',
      idempotencyKey: 'idem-1',
      ...overrides,
    },
    items: [{ productId: 'p1', quantity: 1 }],
  };
}

describe('OfflineOrderManager local persistence', () => {
  beforeEach(() => {
    mockMemory.clear();
    resetOfflineOrderManagerForTests();
  });

  it('saves and reloads pending offline orders', async () => {
    const manager = new OfflineOrderManager(new AsyncStorageAdapter(), {
      autoSync: false,
      isOnlineChecker: async () => false,
    });

    const saved = await manager.saveOrder(cashSnapshot(), 'cash');
    expect(saved.id).toBeTruthy();
    expect(await manager.getPendingCount()).toBe(1);

    const pending = await manager.getPendingOrders();
    expect(pending).toHaveLength(1);
    expect(pending[0]).toMatchObject({
      paymentMethod: 'cash',
      orderTotal: 9.9,
      status: 'pending',
    });
    expect(pending[0].orderData).toMatchObject({
      paymentRequest: { idempotencyKey: 'idem-1', cashRegisterId: 'cr-1' },
    });
  });

  it('rejects voucher plaintext from the offline queue', async () => {
    const manager = new OfflineOrderManager(new AsyncStorageAdapter(), { autoSync: false });

    await expect(
      manager.saveOrder(
        {
          paymentRequest: {
            customerId: 'c1',
            items: [{ productId: 'p1', quantity: 1, taxType: 'standard' }],
            payment: { method: 'voucher', tseRequired: false, voucherCode: 'SECRET' },
            tableNumber: 1,
            totalAmount: 5,
            cashRegisterId: 'cr-1',
          },
        },
        'voucher'
      )
    ).rejects.toThrow(/Gutschein/);

    expect(await manager.getPendingCount()).toBe(0);
  });

  it('rejects voucher method without a code when offline Gutschein is disabled', async () => {
    const manager = new OfflineOrderManager(new AsyncStorageAdapter(), { autoSync: false });

    await expect(
      manager.saveOrder(
        {
          paymentRequest: {
            customerId: 'c1',
            items: [{ productId: 'p1', quantity: 1, taxType: 'standard' }],
            payment: { method: 'voucher', tseRequired: false },
            tableNumber: 1,
            totalAmount: 5,
            cashRegisterId: 'cr-1',
          },
        },
        'voucher'
      )
    ).rejects.toThrow(/Gutschein/);

    expect(await manager.getPendingCount()).toBe(0);
  });

  it('deduplicates by payment idempotency key', async () => {
    const manager = new OfflineOrderManager(new AsyncStorageAdapter(), { autoSync: false });
    const first = await manager.saveOrder(cashSnapshot({ idempotencyKey: 'same-key' }), 'cash');
    const second = await manager.saveOrder(cashSnapshot({ idempotencyKey: 'same-key' }), 'cash');

    expect(second.id).toBe(first.id);
    expect(await manager.getPendingCount()).toBe(1);
  });
});
