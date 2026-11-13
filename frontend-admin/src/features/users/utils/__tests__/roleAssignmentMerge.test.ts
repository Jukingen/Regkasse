/**
 * Role assignment merge logic – full catalog vs assigned-only for checked state.
 * Ensures UI is never driven by assigned subset for the visible list; assigned only for checked.
 */
import { describe, expect, it } from 'vitest';

import { getAssignedRoleIdsFromUser, isRoleChecked } from '../roleAssignmentMerge';

describe('roleAssignmentMerge', () => {
  describe('getAssignedRoleIdsFromUser', () => {
    it('returns empty array when user is null or undefined', () => {
      expect(getAssignedRoleIdsFromUser(null)).toEqual([]);
      expect(getAssignedRoleIdsFromUser(undefined)).toEqual([]);
    });

    it('returns empty array when user has no role or empty role', () => {
      expect(getAssignedRoleIdsFromUser({})).toEqual([]);
      expect(getAssignedRoleIdsFromUser({ role: null })).toEqual([]);
      expect(getAssignedRoleIdsFromUser({ role: '' })).toEqual([]);
    });

    it('returns single-element array with user role (single-role model)', () => {
      expect(getAssignedRoleIdsFromUser({ role: 'Manager' })).toEqual(['Manager']);
      expect(getAssignedRoleIdsFromUser({ role: 'SuperAdmin' })).toEqual(['SuperAdmin']);
      expect(getAssignedRoleIdsFromUser({ role: 'Cashier' })).toEqual(['Cashier']);
    });
  });

  describe('isRoleChecked', () => {
    it('returns false when roleIdOrName is empty or assignedRoleIds is empty', () => {
      expect(isRoleChecked('', [])).toBe(false);
      expect(isRoleChecked('Manager', [])).toBe(false);
    });

    it('returns true only when role is in assignedRoleIds', () => {
      expect(isRoleChecked('Manager', ['Manager'])).toBe(true);
      expect(isRoleChecked('SuperAdmin', ['SuperAdmin'])).toBe(true);
      expect(isRoleChecked('Manager', ['Cashier', 'Manager'])).toBe(true);
    });

    it('returns false when role is not in assignedRoleIds', () => {
      expect(isRoleChecked('Manager', ['Cashier'])).toBe(false);
      expect(isRoleChecked('Waiter', ['Manager', 'Cashier'])).toBe(false);
    });
  });

  describe('catalog-driven merge semantics', () => {
    it('full catalog length is independent of assigned (display list from catalog, not assigned)', () => {
      const fullCatalog = ['SuperAdmin', 'Manager', 'Cashier', 'Waiter', 'Kitchen'];
      const assignedRoleIds = getAssignedRoleIdsFromUser({ role: 'Manager' });
      // Visible list must be full catalog (5); checked is only Manager.
      expect(fullCatalog.length).toBe(5);
      expect(assignedRoleIds).toEqual(['Manager']);
      expect(fullCatalog.filter((r) => isRoleChecked(r, assignedRoleIds))).toEqual(['Manager']);
    });

    it('user switch: assigned comes from new user only (no stale state)', () => {
      const userA = { role: 'Manager' as const };
      const userB = { role: 'Cashier' as const };
      const catalog = ['SuperAdmin', 'Manager', 'Cashier'];
      const assignedA = getAssignedRoleIdsFromUser(userA);
      const assignedB = getAssignedRoleIdsFromUser(userB);
      expect(assignedA).toEqual(['Manager']);
      expect(assignedB).toEqual(['Cashier']);
      expect(isRoleChecked('Manager', assignedB)).toBe(false);
      expect(isRoleChecked('Cashier', assignedB)).toBe(true);
    });
  });
});
