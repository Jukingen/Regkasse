/**
 * Table selection contract – regression protection for POS table switching.
 *
 * Invariants:
 * - currentCart must always be derived as getCartForTableNumber(cartsByTable, activeTableId).
 * - Switching activeTableId must not change cartsByTable; only which cart is "current" changes.
 * - Adding items must not change activeTableId; table selection is independent of cart content.
 */

import type { Cart, CartsByTable } from '../contexts/CartContext';

/** Pure: returns the cart for the given table; empty cart if none. Single source for "current cart" derivation. */
export function getCartForTableNumber(cartsByTable: CartsByTable, tableNumber: number): Cart {
  return cartsByTable[tableNumber] ?? { items: [] };
}

/** Table numbers considered valid for selection (POS range). */
export const VALID_TABLE_NUMBERS = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] as const;

export function isValidTableNumber(n: number): boolean {
  return Number.isInteger(n) && n >= 1 && n <= 10;
}
