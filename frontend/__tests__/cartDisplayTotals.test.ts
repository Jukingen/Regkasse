/**
 * Regression: cleared / empty carts must never show a non-zero grand total.
 * Root cause: clearCart used to wipe items but keep grandTotalGross via object spread.
 * Also: empty table switch must zero totals (fetch `items: []` is truthy in JS).
 */

import type { Cart, CartsByTable } from '../contexts/CartContext';
import {
  createEmptyCart,
  EMPTY_CART,
  getCartDisplayTotals,
  getCartForTableNumber,
  isCartEmpty,
} from '../utils/tableCartUtils';

describe('getCartDisplayTotals / clearCart totals reset', () => {
  it('createEmptyCart zeros all monetary totals', () => {
    const empty = createEmptyCart();
    expect(empty.items).toEqual([]);
    expect(empty.subtotalGross).toBe(0);
    expect(empty.includedTaxTotal).toBe(0);
    expect(empty.grandTotalGross).toBe(0);
    expect(empty.cartId).toBeUndefined();
    expect(empty.cartRowId).toBeUndefined();
  });

  it('getCartDisplayTotals returns 0 when items are empty even if stale totals remain', () => {
    const staleAfterBrokenClear: Cart = {
      items: [],
      subtotalGross: 42.5,
      includedTaxTotal: 7.08,
      grandTotalGross: 42.5,
      taxSummary: [
        { taxType: 1, taxRatePct: 20, netAmount: 35.42, taxAmount: 7.08, grossAmount: 42.5 },
      ],
    };

    const totals = getCartDisplayTotals(staleAfterBrokenClear);
    expect(totals.itemCount).toBe(0);
    expect(totals.subtotalGross).toBe(0);
    expect(totals.includedTaxTotal).toBe(0);
    expect(totals.grandTotalGross).toBe(0);
    expect(totals.taxSummary).toBeUndefined();
  });

  it('getCartDisplayTotals reads backend totals when cart has items', () => {
    const cart: Cart = {
      items: [
        {
          productId: 'p1',
          productName: 'Kaffee',
          qty: 2,
          unitPrice: 2.5,
          totalPrice: 5,
        },
      ],
      subtotalGross: 5,
      includedTaxTotal: 0.83,
      grandTotalGross: 5,
    };

    const totals = getCartDisplayTotals(cart);
    expect(totals.itemCount).toBe(2);
    expect(totals.grandTotalGross).toBe(5);
    expect(totals.subtotalGross).toBe(5);
  });

  it('simulates clearCart local state: empty cart snapshot shows 0 in UI helpers', () => {
    const afterClear = createEmptyCart();
    expect(getCartDisplayTotals(afterClear).grandTotalGross).toBe(0);
  });
});

describe('empty table switch totals', () => {
  it('missing table slot resolves to EMPTY_CART with zero totals', () => {
    const cartsByTable: CartsByTable = {
      1: {
        items: [{ productId: 'p1', productName: 'Kaffee', qty: 1, totalPrice: 3 }],
        grandTotalGross: 3,
      },
    };
    const currentCart = getCartForTableNumber(cartsByTable, 2);
    expect(currentCart).toBe(EMPTY_CART);
    expect(isCartEmpty(currentCart)).toBe(true);
    expect(getCartDisplayTotals(currentCart).grandTotalGross).toBe(0);
  });

  it('switch optimistic empty path: stale totals on empty lines are replaced by createEmptyCart', () => {
    const cartsByTable: CartsByTable = {
      1: {
        items: [{ productId: 'p1', productName: 'Kaffee', qty: 1, totalPrice: 12 }],
        grandTotalGross: 12,
      },
      2: {
        items: [],
        grandTotalGross: 12, // stale leftover (e.g. bad clear / empty items[] fetch branch)
        subtotalGross: 12,
      },
    };

    // Same logic as switchTable empty-table branch
    const tableNumber = 2;
    const next = isCartEmpty(cartsByTable[tableNumber])
      ? { ...cartsByTable, [tableNumber]: createEmptyCart() }
      : cartsByTable;

    expect(isCartEmpty(next[2])).toBe(true);
    expect(next[2].grandTotalGross).toBe(0);
    expect(getCartDisplayTotals(next[2]).grandTotalGross).toBe(0);
    // Table 1 order untouched
    expect(next[1].items).toHaveLength(1);
    expect(next[1].grandTotalGross).toBe(12);
  });

  it('keeps cached lines when switching to a table that already has an order', () => {
    const cartsByTable: CartsByTable = {
      2: {
        items: [{ productId: 'p2', productName: 'Tee', qty: 1, totalPrice: 2.5 }],
        grandTotalGross: 2.5,
      },
    };
    const tableNumber = 2;
    const next = isCartEmpty(cartsByTable[tableNumber])
      ? { ...cartsByTable, [tableNumber]: createEmptyCart() }
      : cartsByTable;

    expect(next).toBe(cartsByTable);
    expect(getCartDisplayTotals(next[2]).grandTotalGross).toBe(2.5);
  });
});
