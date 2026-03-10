/**
 * Regression tests for POS table switching.
 *
 * Ensures:
 * - Selecting table 1, adding product, then switching to table 2: selected table UI and cart context follow table 2.
 * - Current cart is always derived from activeTableId only; no cross-table cart display.
 * - Can switch back to table 1; table 1's cart is unchanged.
 */

import {
  getCartForTableNumber,
  isValidTableNumber,
  VALID_TABLE_NUMBERS,
} from '../utils/tableCartUtils';
import type { CartsByTable, Cart } from '../contexts/CartContext';

describe('Table switching contract (regression)', () => {
  const cartTable1WithItem: Cart = {
    items: [
      {
        productId: 'p1',
        productName: 'Kaffee',
        qty: 1,
        unitPrice: 2.5,
        totalPrice: 2.5,
      },
    ],
    updatedAt: Date.now(),
  };

  const cartTable2Empty: Cart = { items: [] };

  const cartsByTable: CartsByTable = {
    1: cartTable1WithItem,
    2: cartTable2Empty,
  };

  describe('getCartForTableNumber', () => {
    it('returns table 1 cart when activeTableId is 1', () => {
      const currentCart = getCartForTableNumber(cartsByTable, 1);
      expect(currentCart.items).toHaveLength(1);
      expect(currentCart.items[0].productId).toBe('p1');
    });

    it('returns table 2 cart (empty) when activeTableId is 2', () => {
      const currentCart = getCartForTableNumber(cartsByTable, 2);
      expect(currentCart.items).toHaveLength(0);
    });

    it('returns empty cart for table with no entry in cartsByTable', () => {
      const currentCart = getCartForTableNumber(cartsByTable, 3);
      expect(currentCart).toEqual({ items: [] });
    });

    it('does not mutate or mix table data: table 1 cart unchanged when reading table 2', () => {
      const forTable2 = getCartForTableNumber(cartsByTable, 2);
      const forTable1 = getCartForTableNumber(cartsByTable, 1);
      expect(forTable2.items).toHaveLength(0);
      expect(forTable1.items).toHaveLength(1);
      expect(cartsByTable[1].items).toHaveLength(1);
    });
  });

  describe('scenario: select table 1 → add product → switch to table 2', () => {
    it('after switch, current cart is table 2 (not table 1)', () => {
      const activeTableId = 2; // user switched to table 2
      const currentCart = getCartForTableNumber(cartsByTable, activeTableId);
      expect(currentCart.items).toHaveLength(0);
      expect(currentCart).toBe(cartTable2Empty);
    });

    it('table 1 cart still has the product (no cross-table overwrite)', () => {
      expect(getCartForTableNumber(cartsByTable, 1).items).toHaveLength(1);
    });
  });

  describe('scenario: switch back to table 1', () => {
    it('current cart is table 1 again with existing items', () => {
      const currentCart = getCartForTableNumber(cartsByTable, 1);
      expect(currentCart.items).toHaveLength(1);
      expect(currentCart.items[0].productName).toBe('Kaffee');
    });
  });

  describe('isValidTableNumber', () => {
    it('accepts 1..10', () => {
      VALID_TABLE_NUMBERS.forEach((n) => expect(isValidTableNumber(n)).toBe(true));
    });

    it('rejects 0, 11, non-integers', () => {
      expect(isValidTableNumber(0)).toBe(false);
      expect(isValidTableNumber(11)).toBe(false);
      expect(isValidTableNumber(1.5)).toBe(false);
    });
  });
});
