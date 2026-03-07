/**
 * Unit tests for POS modifier selection helpers.
 *
 * Coverage:
 * 1. single-select group behaves like radio
 * 2. multi-select group behaves like checkbox
 * 3. selecting a new radio option replaces the previous one
 * 4. checkbox selection toggles on/off
 * 5. max_selection prevents additional unchecked options
 * 6. required/min_selection validation fails correctly
 * 7. valid groups pass validation
 * 8. validateAllGroups returns correct error state
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

function group(options: {
  id: string;
  minSelections?: number;
  maxSelections?: number | null;
  isRequired?: boolean;
  productIds?: string[];
}): ModifierGroupSelectionShape {
  const {
    id,
    minSelections = 0,
    maxSelections = null,
    isRequired = false,
    productIds = [],
  } = options;
  return {
    id,
    minSelections,
    maxSelections,
    isRequired,
    products: productIds.map((productId) => ({ productId })),
  };
}

describe('modifierSelectionUtils', () => {
  describe('1. single-select group behaves like radio', () => {
    it('returns radio when min=1 and max=1', () => {
      const g = group({ id: 'g1', minSelections: 1, maxSelections: 1, productIds: ['p1', 'p2'] });
      expect(getGroupControlType(g)).toBe('radio');
    });

    it('only min=1 max=1 is treated as radio; min=0 max=1 is checkbox', () => {
      const g = group({ id: 'g1', minSelections: 0, maxSelections: 1, productIds: ['p1'] });
      expect(getGroupControlType(g)).toBe('checkbox');
    });
  });

  describe('2. multi-select group behaves like checkbox', () => {
    it('returns checkbox when max > 1', () => {
      const g = group({ id: 'g1', minSelections: 0, maxSelections: 3, productIds: ['p1', 'p2'] });
      expect(getGroupControlType(g)).toBe('checkbox');
    });

    it('returns checkbox when max is null (unlimited)', () => {
      const g = group({ id: 'g1', minSelections: 0, maxSelections: null, productIds: ['p1'] });
      expect(getGroupControlType(g)).toBe('checkbox');
    });
  });

  describe('3. selecting a new radio option replaces the previous one', () => {
    it('selecting p2 when p1 is selected leaves only p2 in group', () => {
      const g = group({ id: 'g1', minSelections: 1, maxSelections: 1, productIds: ['p1', 'p2', 'p3'] });
      const prev = new Set<string>(['p1']);
      const next = toggleSelectionInGroup(prev, g, 'p2');
      expect(next.has('p1')).toBe(false);
      expect(next.has('p2')).toBe(true);
      expect(next.has('p3')).toBe(false);
    });

    it('selecting same radio option again deselects it', () => {
      const g = group({ id: 'g1', minSelections: 1, maxSelections: 1, productIds: ['p1', 'p2'] });
      const prev = new Set<string>(['p1']);
      const next = toggleSelectionInGroup(prev, g, 'p1');
      expect(next.has('p1')).toBe(false);
    });
  });

  describe('4. checkbox selection toggles on/off', () => {
    it('toggling an unselected option adds it', () => {
      const g = group({ id: 'g1', minSelections: 0, maxSelections: 2, productIds: ['p1', 'p2'] });
      const next = toggleSelectionInGroup(new Set(), g, 'p1');
      expect(next.has('p1')).toBe(true);
    });

    it('toggling a selected option removes it', () => {
      const g = group({ id: 'g1', minSelections: 0, maxSelections: 2, productIds: ['p1', 'p2'] });
      const next = toggleSelectionInGroup(new Set(['p1']), g, 'p1');
      expect(next.has('p1')).toBe(false);
    });
  });

  describe('5. max_selection prevents additional unchecked options', () => {
    it('toggle does not add when group is at max', () => {
      const g = group({ id: 'g1', minSelections: 0, maxSelections: 2, productIds: ['p1', 'p2', 'p3'] });
      const atMax = new Set<string>(['p1', 'p2']);
      const next = toggleSelectionInGroup(atMax, g, 'p3');
      expect(next.has('p3')).toBe(false);
      expect(next.size).toBe(2);
    });

    it('isOptionDisabled returns true for unselected option when at max', () => {
      const g = group({ id: 'g1', minSelections: 0, maxSelections: 1, productIds: ['p1', 'p2'] });
      const selected = new Set<string>(['p1']);
      expect(isOptionDisabled(g, selected, 'p2')).toBe(true);
    });

    it('isOptionDisabled returns false for already selected option when at max', () => {
      const g = group({ id: 'g1', minSelections: 0, maxSelections: 1, productIds: ['p1', 'p2'] });
      const selected = new Set<string>(['p1']);
      expect(isOptionDisabled(g, selected, 'p1')).toBe(false);
    });
  });

  describe('6. required/min_selection validation fails correctly', () => {
    it('fails when selected count is below minSelections', () => {
      const g = group({ id: 'g1', minSelections: 2, maxSelections: 3, productIds: ['p1', 'p2', 'p3'] });
      const result = validateGroup(g, new Set(['p1']));
      expect(result.valid).toBe(false);
      expect(result.message).toContain('Mindestens');
      expect(result.message).toContain('2');
    });

    it('fails when no selection and minSelections is 1', () => {
      const g = group({ id: 'g1', minSelections: 1, maxSelections: 1, productIds: ['p1'] });
      const result = validateGroup(g, new Set());
      expect(result.valid).toBe(false);
      expect(result.message).toBeDefined();
    });

    it('isGroupRequired is true when minSelections > 0', () => {
      expect(isGroupRequired(group({ id: 'g1', minSelections: 1, maxSelections: 1 }))).toBe(true);
    });

    it('isGroupRequired is true when isRequired is true', () => {
      expect(isGroupRequired(group({ id: 'g1', minSelections: 0, maxSelections: null, isRequired: true }))).toBe(true);
    });
  });

  describe('7. valid groups pass validation', () => {
    it('passes when count equals min', () => {
      const g = group({ id: 'g1', minSelections: 1, maxSelections: 2, productIds: ['p1', 'p2'] });
      const result = validateGroup(g, new Set(['p1']));
      expect(result.valid).toBe(true);
      expect(result.message).toBeUndefined();
    });

    it('passes when count is between min and max', () => {
      const g = group({ id: 'g1', minSelections: 1, maxSelections: 3, productIds: ['p1', 'p2', 'p3'] });
      const result = validateGroup(g, new Set(['p1', 'p2']));
      expect(result.valid).toBe(true);
    });

    it('passes when count equals max', () => {
      const g = group({ id: 'g1', minSelections: 0, maxSelections: 2, productIds: ['p1', 'p2'] });
      const result = validateGroup(g, new Set(['p1', 'p2']));
      expect(result.valid).toBe(true);
    });

    it('passes when min=0 and no selection', () => {
      const g = group({ id: 'g1', minSelections: 0, maxSelections: 2, productIds: ['p1'] });
      const result = validateGroup(g, new Set());
      expect(result.valid).toBe(true);
    });
  });

  describe('8. validateAllGroups returns correct error state', () => {
    it('returns valid true and empty errors when all groups pass', () => {
      const groups = [
        group({ id: 'g1', minSelections: 0, maxSelections: 1, productIds: ['p1'] }),
        group({ id: 'g2', minSelections: 0, maxSelections: null, productIds: ['p2'] }),
      ];
      const result = validateAllGroups(groups, new Set());
      expect(result.valid).toBe(true);
      expect(result.errors).toEqual([]);
    });

    it('returns valid false and errors array when at least one group fails', () => {
      const groups = [
        group({ id: 'g1', minSelections: 0, maxSelections: 1, productIds: ['p1'] }),
        group({ id: 'g2', minSelections: 1, maxSelections: 1, productIds: ['p2'] }),
      ];
      const result = validateAllGroups(groups, new Set());
      expect(result.valid).toBe(false);
      expect(result.errors.length).toBeGreaterThan(0);
      expect(result.errors.every((e) => e.groupId && e.message)).toBe(true);
    });

    it('includes failing group id and message in each error', () => {
      const groups = [
        group({ id: 'required-group', minSelections: 1, maxSelections: 1, productIds: ['p1'] }),
      ];
      const result = validateAllGroups(groups, new Set());
      expect(result.valid).toBe(false);
      expect(result.errors).toHaveLength(1);
      expect(result.errors[0].groupId).toBe('required-group');
      expect(result.errors[0].message).toBeDefined();
      expect(result.errors[0].message!.length).toBeGreaterThan(0);
    });

    it('returns all failing groups in errors (not only the first)', () => {
      const groups = [
        group({ id: 'g1', minSelections: 1, maxSelections: 1, productIds: ['p1'] }),
        group({ id: 'g2', minSelections: 1, maxSelections: 1, productIds: ['p2'] }),
      ];
      const result = validateAllGroups(groups, new Set());
      expect(result.valid).toBe(false);
      expect(result.errors).toHaveLength(2);
      expect(result.errors.map((e) => e.groupId)).toContain('g1');
      expect(result.errors.map((e) => e.groupId)).toContain('g2');
    });
  });
});
