/**
 * Table selection contract – regression protection for POS table switching.
 *
 * Invariants:
 * - currentCart must always be derived as getCartForTableNumber(cartsByTable, activeTableId).
 * - Switching activeTableId must not change cartsByTable; only which cart is "current" changes.
 * - Adding items must not change activeTableId; table selection is independent of cart content.
 */

import type { Cart, CartsByTable } from '../contexts/CartContext';

/** Stable empty cart for missing table slots (zero totals — never flash a previous table amount). */
export const EMPTY_CART: Cart = Object.freeze({
  items: Object.freeze([]) as unknown as Cart['items'],
  subtotalGross: 0,
  subtotalNet: 0,
  includedTaxTotal: 0,
  grandTotalGross: 0,
});

/** Fresh empty cart snapshot used after clear / empty fetch / empty table switch. */
export function createEmptyCart(overrides?: Partial<Cart>): Cart {
  return {
    items: [],
    updatedAt: Date.now(),
    subtotalGross: 0,
    subtotalNet: 0,
    includedTaxTotal: 0,
    grandTotalGross: 0,
    taxSummary: undefined,
    cartRowId: undefined,
    cartId: undefined,
    ...overrides,
  };
}

/**
 * Backend fields for UI — no FE tax math.
 * Empty items always report total 0 (guards stale grandTotalGross left by a clear that only wiped lines).
 */
export function getCartDisplayTotals(cart: Cart | null | undefined): {
  subtotalGross: number;
  includedTaxTotal: number;
  grandTotalGross: number;
  itemCount: number;
  taxSummary?: Cart['taxSummary'];
} {
  const items = cart?.items ?? [];
  const itemCount = items.reduce((sum, i) => sum + (i.qty ?? 0), 0);
  if (items.length === 0) {
    return {
      subtotalGross: 0,
      includedTaxTotal: 0,
      grandTotalGross: 0,
      itemCount: 0,
      taxSummary: undefined,
    };
  }
  return {
    subtotalGross: cart?.subtotalGross ?? 0,
    includedTaxTotal: cart?.includedTaxTotal ?? 0,
    grandTotalGross: cart?.grandTotalGross ?? 0,
    itemCount,
    taxSummary: cart?.taxSummary,
  };
}

/** True when cart has no order lines (empty table / cleared cart). */
export function isCartEmpty(cart: Cart | null | undefined): boolean {
  return (cart?.items?.length ?? 0) === 0;
}

/** Pure: returns the cart for the given table; empty cart if none. Single source for "current cart" derivation. */
export function getCartForTableNumber(cartsByTable: CartsByTable, tableNumber: number): Cart {
  return cartsByTable[tableNumber] ?? EMPTY_CART;
}

/** Table numbers considered valid for selection (POS range). */
export const VALID_TABLE_NUMBERS = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] as const;

export function isValidTableNumber(n: number): boolean {
  return Number.isInteger(n) && n >= 1 && n <= 10;
}
