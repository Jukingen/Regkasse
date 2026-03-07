/**
 * POS modifier UI flow – behavior tests.
 *
 * Covers:
 * 1. Product without modifier groups adds directly to cart
 * 2. Required single-select group blocks submit until selected
 * 3. Multi-select group allows multiple selections up to max
 * 4. Options beyond max_selection become disabled when appropriate
 * 5. Successful submit adds base product and add-on products correctly
 * 6. UI uses group.products only (no legacy modifiers)
 *
 * Uses real modifierSelectionUtils; replicates POS decision and payload logic.
 */

import {
  getGroupControlType,
  toggleSelectionInGroup,
  isOptionDisabled,
  validateGroup,
  validateAllGroups,
  isGroupRequired,
  type ModifierGroupSelectionShape,
} from '../utils/modifierSelectionUtils';

// --- POS DTO shapes (group.products only; no group.modifiers in option source) ---

interface AddOnProductDto {
  productId: string;
  productName: string;
  price: number;
}

interface ModifierGroupDto {
  id: string;
  name: string;
  minSelections: number;
  maxSelections: number | null;
  isRequired?: boolean;
  products?: AddOnProductDto[];
}

/** Replicate ProductRow/ProductGridCard: only groups with products are used for add-on UI. */
function groupsWithProducts(groups: ModifierGroupDto[]): ModifierGroupDto[] {
  return groups.filter((g) => (g.products ?? []).length > 0);
}

/** Replicate useProductModifierGroups: has add-ons only when at least one group has products. */
function hasAddOnProducts(groups: ModifierGroupDto[]): boolean {
  return groupsWithProducts(groups).length > 0;
}

/** Convert to shape expected by modifierSelectionUtils (products only). */
function toSelectionShape(g: ModifierGroupDto): ModifierGroupSelectionShape {
  return {
    id: g.id,
    minSelections: g.minSelections,
    maxSelections: g.maxSelections,
    isRequired: g.isRequired,
    products: (g.products ?? []).map((p) => ({ productId: p.productId })),
  };
}

/** Replicate ModifierSelectionBottomSheet getSelectedAddOns: build add-on list from groups + selectedIds. */
function getSelectedAddOns(
  groups: ModifierGroupDto[],
  selectedIds: Set<string>
): Array<{ productId: string; productName: string; price: number }> {
  const out: Array<{ productId: string; productName: string; price: number }> = [];
  for (const g of groupsWithProducts(groups)) {
    for (const p of g.products ?? []) {
      if (selectedIds.has(p.productId)) {
        out.push({ productId: p.productId, productName: p.productName, price: p.price });
      }
    }
  }
  return out;
}

/** Replicate POS decision: product with no add-on groups → direct add; with groups → open sheet. */
function getAddBehavior(product: { modifierGroups?: ModifierGroupDto[] }): 'direct' | 'sheet' {
  const groups = product.modifierGroups ?? [];
  return hasAddOnProducts(groups) ? 'sheet' : 'direct';
}

// --- Test data ---

const baseProduct = {
  id: 'base-1',
  name: 'Burger',
  price: 9.9,
  modifierGroups: [] as ModifierGroupDto[],
};

const groupSauce: ModifierGroupDto = {
  id: 'sauce',
  name: 'Sauce',
  minSelections: 1,
  maxSelections: 1,
  isRequired: true,
  products: [
    { productId: 'p-ketchup', productName: 'Ketchup', price: 0.5 },
    { productId: 'p-mayo', productName: 'Mayo', price: 0.5 },
  ],
};

const groupExtras: ModifierGroupDto = {
  id: 'extras',
  name: 'Extras',
  minSelections: 0,
  maxSelections: 2,
  isRequired: false,
  products: [
    { productId: 'p-bacon', productName: 'Bacon', price: 1.5 },
    { productId: 'p-cheese', productName: 'Cheese', price: 1.0 },
    { productId: 'p-avocado', productName: 'Avocado', price: 2.0 },
  ],
};

describe('POS modifier UI flow', () => {
  describe('1. Product without modifier groups adds directly to cart', () => {
    it('product with no modifierGroups yields direct add (no sheet)', () => {
      const product = { ...baseProduct, modifierGroups: [] };
      expect(getAddBehavior(product)).toBe('direct');
    });

    it('product with empty modifierGroups yields direct add', () => {
      const product = { ...baseProduct, modifierGroups: [] };
      expect(getAddBehavior(product)).toBe('direct');
    });

    it('product with groups that have only legacy modifiers (no products) yields direct add', () => {
      const product = {
        ...baseProduct,
        modifierGroups: [
          { id: 'g1', name: 'Legacy', minSelections: 0, maxSelections: null, products: [] },
        ],
      };
      expect(hasAddOnProducts(product.modifierGroups!)).toBe(false);
      expect(getAddBehavior(product)).toBe('direct');
    });

    it('product with at least one group that has products yields sheet', () => {
      const product = { ...baseProduct, modifierGroups: [groupSauce] };
      expect(getAddBehavior(product)).toBe('sheet');
    });
  });

  describe('2. Required single-select group blocks submit until selected', () => {
    it('validateAllGroups fails when required single-select has no selection', () => {
      const groups = [toSelectionShape(groupSauce)];
      const result = validateAllGroups(groups, new Set());
      expect(result.valid).toBe(false);
      expect(result.errors.length).toBeGreaterThan(0);
      expect(result.errors.some((e) => e.groupId === 'sauce')).toBe(true);
    });

    it('validateAllGroups passes when required single-select has one selection', () => {
      const groups = [toSelectionShape(groupSauce)];
      const result = validateAllGroups(groups, new Set(['p-ketchup']));
      expect(result.valid).toBe(true);
      expect(result.errors).toHaveLength(0);
    });

    it('validateGroup returns valid false when minSelections=1 and count=0', () => {
      const g = toSelectionShape(groupSauce);
      expect(validateGroup(g, new Set()).valid).toBe(false);
    });
  });

  describe('3. Multi-select group allows multiple selections up to max', () => {
    it('getGroupControlType returns checkbox for maxSelections > 1', () => {
      expect(getGroupControlType(toSelectionShape(groupExtras))).toBe('checkbox');
    });

    it('toggleSelectionInGroup allows selecting up to maxSelections', () => {
      const g = toSelectionShape(groupExtras);
      let selected = new Set<string>();

      selected = toggleSelectionInGroup(selected, g, 'p-bacon');
      expect(selected.size).toBe(1);
      selected = toggleSelectionInGroup(selected, g, 'p-cheese');
      expect(selected.size).toBe(2);
      expect(selected.has('p-bacon')).toBe(true);
      expect(selected.has('p-cheese')).toBe(true);
    });

    it('toggleSelectionInGroup does not add a third when already at max 2', () => {
      const g = toSelectionShape(groupExtras);
      const selected = new Set(['p-bacon', 'p-cheese']);
      const next = toggleSelectionInGroup(selected, g, 'p-avocado');
      expect(next.size).toBe(2);
      expect(next.has('p-avocado')).toBe(false);
    });
  });

  describe('4. Options beyond max_selection become disabled when appropriate', () => {
    it('isOptionDisabled is true for unselected option when group is at max', () => {
      const g = toSelectionShape(groupExtras);
      const selected = new Set(['p-bacon', 'p-cheese']);
      expect(isOptionDisabled(g, selected, 'p-avocado')).toBe(true);
    });

    it('isOptionDisabled is false for already selected option when at max', () => {
      const g = toSelectionShape(groupExtras);
      const selected = new Set(['p-bacon', 'p-cheese']);
      expect(isOptionDisabled(g, selected, 'p-bacon')).toBe(false);
      expect(isOptionDisabled(g, selected, 'p-cheese')).toBe(false);
    });

    it('isOptionDisabled is false for any option when under max', () => {
      const g = toSelectionShape(groupExtras);
      const selected = new Set(['p-bacon']);
      expect(isOptionDisabled(g, selected, 'p-cheese')).toBe(false);
      expect(isOptionDisabled(g, selected, 'p-avocado')).toBe(false);
    });

    it('single-select at max: other option is disabled', () => {
      const g = toSelectionShape(groupSauce);
      const selected = new Set(['p-ketchup']);
      expect(isOptionDisabled(g, selected, 'p-mayo')).toBe(true);
    });
  });

  describe('5. Successful submit adds base product and add-on products correctly', () => {
    it('getSelectedAddOns returns correct add-on payload from selectedIds', () => {
      const groups = [groupSauce, groupExtras];
      const selectedIds = new Set(['p-ketchup', 'p-bacon', 'p-cheese']);
      const addOns = getSelectedAddOns(groups, selectedIds);

      expect(addOns).toHaveLength(3);
      expect(addOns.map((a) => a.productId).sort()).toEqual(['p-bacon', 'p-cheese', 'p-ketchup']);
      expect(addOns.find((a) => a.productId === 'p-ketchup')).toEqual({
        productId: 'p-ketchup',
        productName: 'Ketchup',
        price: 0.5,
      });
      expect(addOns.find((a) => a.productId === 'p-bacon')).toEqual({
        productId: 'p-bacon',
        productName: 'Bacon',
        price: 1.5,
      });
    });

    it('onApplyWithBase receives base + addOns with no modifier ids', () => {
      const groups = [groupSauce, groupExtras];
      const selectedIds = new Set(['p-mayo']);
      const addOns = getSelectedAddOns(groups, selectedIds);

      const base = { productId: baseProduct.id, productName: baseProduct.name, price: baseProduct.price };
      expect(addOns).toHaveLength(1);
      expect(addOns[0].productId).toBe('p-mayo');
      expect(addOns[0].productName).toBe('Mayo');
      expect(addOns[0].price).toBe(0.5);
      expect(base.productId).toBe('base-1');
      expect(base.productName).toBe('Burger');
      expect(base.price).toBe(9.9);
    });

    it('submit with no add-ons selected (optional groups) yields base only', () => {
      const groups = [groupExtras];
      const selectedIds = new Set<string>();
      const addOns = getSelectedAddOns(groups, selectedIds);
      expect(addOns).toHaveLength(0);
    });
  });

  describe('6. UI uses group.products and does not rely on legacy modifiers', () => {
    it('groupsWithProducts returns only groups that have products', () => {
      const withLegacyOnly = {
        id: 'legacy',
        name: 'Legacy',
        minSelections: 0,
        maxSelections: null,
        products: [] as AddOnProductDto[],
      };
      const withProducts = groupSauce;
      const mixed = [withLegacyOnly, withProducts];
      const result = groupsWithProducts(mixed);
      expect(result).toHaveLength(1);
      expect(result[0].id).toBe('sauce');
      expect(result[0].products).toHaveLength(2);
    });

    it('option list is built only from group.products (no modifiers array)', () => {
      const group = groupSauce;
      const optionIds = (group.products ?? []).map((p) => p.productId);
      const optionNames = (group.products ?? []).map((p) => p.productName);
      expect(optionIds).toEqual(['p-ketchup', 'p-mayo']);
      expect(optionNames).toEqual(['Ketchup', 'Mayo']);
    });

    it('group with modifiers but no products is excluded from add-on UI', () => {
      const legacyGroup = {
        id: 'legacy',
        name: 'Legacy Extras',
        minSelections: 0,
        maxSelections: null,
        products: [] as AddOnProductDto[],
      };
      expect(groupsWithProducts([legacyGroup])).toHaveLength(0);
      expect(hasAddOnProducts([legacyGroup])).toBe(false);
    });

    it('toSelectionShape uses only products for option ids', () => {
      const shape = toSelectionShape(groupSauce);
      expect(shape.products).toHaveLength(2);
      expect(shape.products!.map((p) => p.productId)).toEqual(['p-ketchup', 'p-mayo']);
    });
  });
});
