import { describe, it, expect, beforeEach, jest } from '@jest/globals';

const mem = new Map<string, string>();

jest.mock('../utils/storage', () => ({
  storage: {
    getItem: async (key: string) => mem.get(key) ?? null,
    setItem: async (key: string, value: string) => {
      mem.set(key, value);
    },
    removeItem: async (key: string) => {
      mem.delete(key);
    },
    multiRemove: async (keys: string[]) => {
      keys.forEach((k) => mem.delete(k));
    },
    clear: async () => {
      mem.clear();
    },
    getAllKeys: async () => Array.from(mem.keys()),
  },
}));

import {
  enqueueCartMutation,
  getPendingCartMutations,
  countPendingCartMutations,
  clearCartMutationQueueForTests,
} from '../services/cart/pendingCartMutationQueue';

describe('pendingCartMutationQueue', () => {
  beforeEach(async () => {
    mem.clear();
    await clearCartMutationQueueForTests();
  });

  it('enqueues add_item mutation with monotonic sequence per table', async () => {
    const id1 = await enqueueCartMutation(3, {
      kind: 'add_item',
      productId: 'p1',
      quantity: 1,
    });
    const id2 = await enqueueCartMutation(3, {
      kind: 'add_item',
      productId: 'p2',
      quantity: 2,
    });

    expect(id1).not.toBe(id2);
    const pending = await getPendingCartMutations();
    expect(pending).toHaveLength(2);
    expect(pending[0].clientSequenceNumber).toBe(1);
    expect(pending[1].clientSequenceNumber).toBe(2);
    expect(pending[0].tableNumber).toBe(3);
  });

  it('deduplicates by idempotencyKey for pending entries', async () => {
    const key = 'idem-add-p1-table-5';
    const id1 = await enqueueCartMutation(
      5,
      { kind: 'add_item', productId: 'p1', quantity: 1 },
      key
    );
    const id2 = await enqueueCartMutation(
      5,
      { kind: 'add_item', productId: 'p1', quantity: 1 },
      key
    );
    expect(id1).toBe(id2);
    expect(await countPendingCartMutations()).toBe(1);
  });

  it('tracks separate sequence counters per table', async () => {
    await enqueueCartMutation(1, { kind: 'clear_cart' });
    await enqueueCartMutation(2, { kind: 'clear_cart' });
    const pending = await getPendingCartMutations();
    const t1 = pending.find((e) => e.tableNumber === 1);
    const t2 = pending.find((e) => e.tableNumber === 2);
    expect(t1?.clientSequenceNumber).toBe(1);
    expect(t2?.clientSequenceNumber).toBe(1);
  });
});
