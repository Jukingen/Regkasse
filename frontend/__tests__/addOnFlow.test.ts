/**
 * Add-on flow unit tests: group.products only (Phase C; legacy group.modifiers fallback removed).
 * Pure logic replicates for ProductRow/GridCard and cart line total.
 * Modifier selection helper tests live in __tests__/modifierSelectionUtils.test.ts.
 */

interface ModifierDto {
  id: string;
  name: string;
  price: number;
}

interface AddOnGroupProductItemDto {
  productId: string;
  productName: string;
  price: number;
}

interface ModifierGroupDto {
  id: string;
  name: string;
  modifiers: ModifierDto[];
  products?: AddOnGroupProductItemDto[];
  minSelections?: number;
  maxSelections?: number | null;
  isRequired?: boolean;
}

// Replicate logic from ProductRow/ProductGridCard
function groupsWithProducts(groups: ModifierGroupDto[]): ModifierGroupDto[] {
  return groups.filter((g) => (g.products ?? []).length > 0);
}

// Replicate logic from useProductModifierGroups (Phase C: products only)
function hasModifiers(groups: ModifierGroupDto[]): boolean {
  return groups.some((g) => (g.products?.length ?? 0) > 0);
}

// Replicate logic from ModifierSelectionBottomSheet/Modal: radio vs checkbox
function isSingleChoiceGroup(g: ModifierGroupDto): boolean {
  const min = g.minSelections ?? 0;
  const max = g.maxSelections;
  return min === 1 && max === 1;
}

describe('addOnFlow', () => {
  describe('groupsWithProducts', () => {
    it('returns groups that have products', () => {
      const groups: ModifierGroupDto[] = [
        {
          id: '1',
          name: 'Saucen',
          modifiers: [],
          products: [
            { productId: 'p1', productName: 'Ketchup', price: 0.5 },
            { productId: 'p2', productName: 'Mayo', price: 0.5 },
          ],
        },
        { id: '2', name: 'Extras', modifiers: [{ id: 'm1', name: 'Extra', price: 1 }], products: [] },
      ];
      const result = groupsWithProducts(groups);
      expect(result).toHaveLength(1);
      expect(result[0].name).toBe('Saucen');
      expect(result[0].products).toHaveLength(2);
    });

    it('returns empty when no group has products', () => {
      const groups: ModifierGroupDto[] = [
        { id: '1', name: 'Extras', modifiers: [{ id: 'm1', name: 'Extra', price: 1 }] },
      ];
      expect(groupsWithProducts(groups)).toHaveLength(0);
    });
  });

  describe('hasModifiers (Phase C: products only)', () => {
    it('returns true when group has products', () => {
      const groups: ModifierGroupDto[] = [
        {
          id: '1',
          name: 'Saucen',
          modifiers: [],
          products: [{ productId: 'p1', productName: 'Ketchup', price: 0.5 }],
        },
      ];
      expect(hasModifiers(groups)).toBe(true);
    });

    it('returns false when group has only legacy modifiers (no products)', () => {
      const groups: ModifierGroupDto[] = [
        { id: '1', name: 'Extras', modifiers: [{ id: 'm1', name: 'Extra', price: 1 }], products: [] },
      ];
      expect(hasModifiers(groups)).toBe(false);
    });

    it('returns false when groups are empty', () => {
      expect(hasModifiers([])).toBe(false);
    });

    it('returns false when groups have neither products nor modifiers', () => {
      const groups: ModifierGroupDto[] = [
        { id: '1', name: 'Empty', modifiers: [] },
      ];
      expect(hasModifiers(groups)).toBe(false);
    });
  });

  describe('isSingleChoiceGroup (radio vs checkbox)', () => {
    it('returns true when min=1 and max=1', () => {
      const g: ModifierGroupDto = {
        id: '1',
        name: 'Sauce',
        modifiers: [],
        products: [{ productId: 'p1', productName: 'Ketchup', price: 0.5 }],
        minSelections: 1,
        maxSelections: 1,
      };
      expect(isSingleChoiceGroup(g)).toBe(true);
    });

    it('returns false when max>1', () => {
      const g: ModifierGroupDto = {
        id: '1',
        name: 'Extras',
        modifiers: [],
        products: [{ productId: 'p1', productName: 'Bacon', price: 1.5 }],
        minSelections: 0,
        maxSelections: 3,
      };
      expect(isSingleChoiceGroup(g)).toBe(false);
    });

    it('returns false when max is null', () => {
      const g: ModifierGroupDto = {
        id: '1',
        name: 'Extras',
        modifiers: [],
        products: [],
        minSelections: 0,
        maxSelections: null,
      };
      expect(isSingleChoiceGroup(g)).toBe(false);
    });
  });

  /** Replicate getCartLineTotal logic: add-ons = flat lines (no modifiers). */
  describe('getCartLineTotal (logic)', () => {
    function getCartLineTotal(item: {
      unitPrice?: number;
      price?: number;
      qty?: number;
      modifiers?: { price: number; quantity?: number }[];
    }): number {
      const base = (item.unitPrice ?? item.price ?? 0) * (item.qty ?? 0);
      const modTotal = (item.modifiers ?? []).reduce(
        (s, m) => s + m.price * (m.quantity ?? 1),
        0
      );
      return base + modTotal;
    }

    it('add-on (no modifiers): line total = unitPrice * qty', () => {
      const item = { productId: 'p1', unitPrice: 1.5, qty: 2, modifiers: undefined };
      expect(getCartLineTotal(item)).toBe(3);
    });

    it('add-on qty 1: line total = unitPrice', () => {
      const item = { productId: 'p1', unitPrice: 0.5, qty: 1 };
      expect(getCartLineTotal(item)).toBe(0.5);
    });

    it('legacy item with modifiers: base + sum(mod.price * mod.qty)', () => {
      const item = {
        unitPrice: 6.9,
        qty: 1,
        modifiers: [
          { price: 0.3, quantity: 1 },
          { price: 0.5, quantity: 2 },
        ],
      };
      expect(getCartLineTotal(item)).toBeCloseTo(8.2, 2);
    });
  });
});
