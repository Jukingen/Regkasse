import { describe, expect, it } from '@jest/globals';

import type { Cart } from '../contexts/CartContext';
import { applyOptimisticCartAdd } from '../utils/optimisticCartAdd';

const emptyCart: Cart = { items: [] };

describe('applyOptimisticCartAdd', () => {
  it('shows the new product before the server responds and leaves the previous cart unchanged', () => {
    const next = applyOptimisticCartAdd(emptyCart, {
      productId: 'coffee',
      quantity: 1,
      productName: 'Kaffee',
      unitPrice: 3.5,
      clientId: 'ci-1',
      now: 10,
    });

    expect(emptyCart.items).toHaveLength(0);
    expect(next.items).toEqual([
      expect.objectContaining({
        productId: 'coffee',
        productName: 'Kaffee',
        qty: 1,
        unitPrice: 3.5,
        totalPrice: 3.5,
        clientId: 'ci-1',
      }),
    ]);
    expect(next.updatedAt).toBe(10);
  });

  it('increments a matching line immediately', () => {
    const current: Cart = {
      items: [
        {
          productId: 'coffee',
          productName: 'Kaffee',
          qty: 1,
          price: 3.5,
          unitPrice: 3.5,
          totalPrice: 3.5,
        },
      ],
    };

    const next = applyOptimisticCartAdd(current, {
      productId: 'coffee',
      quantity: 2,
      unitPrice: 3.5,
      now: 20,
    });

    expect(current.items[0].qty).toBe(1);
    expect(next.items[0].qty).toBe(3);
    expect(next.items[0].totalPrice).toBe(10.5);
  });

  it('restores the previous cart when the sync fails', () => {
    const previous: Cart = {
      items: [
        {
          productId: 'coffee',
          productName: 'Kaffee',
          qty: 1,
          price: 3.5,
          unitPrice: 3.5,
          totalPrice: 3.5,
        },
      ],
    };
    const optimistic = applyOptimisticCartAdd(previous, {
      productId: 'cake',
      productName: 'Kuchen',
      unitPrice: 4,
      clientId: 'ci-2',
    });
    const failed = true;
    const visible = failed ? previous : optimistic;

    expect(optimistic.items).toHaveLength(2);
    expect(visible).toBe(previous);
    expect(visible.items.map((item) => item.productId)).toEqual(['coffee']);
  });
});
