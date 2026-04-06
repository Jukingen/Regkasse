import { describe, expect, it } from 'vitest';
import {
  filterCatalogIdsForInventoryNav,
  parseAdminShowInventoryNavEnv,
} from '../adminInventoryNavUi';

describe('adminInventoryNavUi', () => {
  describe('parseAdminShowInventoryNavEnv', () => {
    it('defaults to visible when unset or empty', () => {
      expect(parseAdminShowInventoryNavEnv(undefined)).toBe(true);
      expect(parseAdminShowInventoryNavEnv('')).toBe(true);
    });

    it('hides for false tokens', () => {
      expect(parseAdminShowInventoryNavEnv('false')).toBe(false);
      expect(parseAdminShowInventoryNavEnv('0')).toBe(false);
    });
  });

  describe('filterCatalogIdsForInventoryNav', () => {
    it('keeps inventory when nav env unset (default visible)', () => {
      const prev = process.env.NEXT_PUBLIC_ADMIN_SHOW_INVENTORY_NAV;
      delete process.env.NEXT_PUBLIC_ADMIN_SHOW_INVENTORY_NAV;
      expect(filterCatalogIdsForInventoryNav(['products', 'inventory', 'pricingRules'])).toEqual([
        'products',
        'inventory',
        'pricingRules',
      ]);
      process.env.NEXT_PUBLIC_ADMIN_SHOW_INVENTORY_NAV = prev;
    });

    it('drops inventory when nav env is false', () => {
      const prev = process.env.NEXT_PUBLIC_ADMIN_SHOW_INVENTORY_NAV;
      process.env.NEXT_PUBLIC_ADMIN_SHOW_INVENTORY_NAV = 'false';
      expect(filterCatalogIdsForInventoryNav(['products', 'inventory', 'pricingRules'])).toEqual([
        'products',
        'pricingRules',
      ]);
      process.env.NEXT_PUBLIC_ADMIN_SHOW_INVENTORY_NAV = prev;
    });
  });
});
