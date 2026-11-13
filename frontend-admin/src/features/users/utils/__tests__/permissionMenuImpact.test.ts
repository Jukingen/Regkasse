import { describe, expect, it } from 'vitest';

import { PERMISSIONS } from '@/shared/auth/permissions';
import { AppPermissions } from '@/shared/auth/permissions';

import {
  getMenuChipsForPermissionGroup,
  getMenuItemsAffectedByPermission,
  getPermissionsAffectingMenu,
  listSidebarMenuFilterOptions,
  permissionUnlocksRequirement,
} from '../permissionMenuImpact';

describe('permissionMenuImpact', () => {
  describe('permissionUnlocksRequirement', () => {
    it('matches exact requirement', () => {
      expect(permissionUnlocksRequirement(PERMISSIONS.CUSTOMER_VIEW, PERMISSIONS.CUSTOMER_VIEW)).toBe(
        true
      );
    });

    it('matches via implication (manage unlocks view)', () => {
      expect(
        permissionUnlocksRequirement(PERMISSIONS.CUSTOMER_MANAGE, PERMISSIONS.CUSTOMER_VIEW)
      ).toBe(true);
    });

    it('returns false for empty / any-auth requirements', () => {
      expect(permissionUnlocksRequirement(PERMISSIONS.USER_VIEW, [])).toBe(false);
      expect(permissionUnlocksRequirement(PERMISSIONS.USER_VIEW, undefined)).toBe(false);
    });
  });

  describe('getMenuItemsAffectedByPermission', () => {
    it('returns staff/users menus for user.view', () => {
      const items = getMenuItemsAffectedByPermission(PERMISSIONS.USER_VIEW);
      expect(items.length).toBeGreaterThan(0);
      expect(items.some((i) => i.path.includes('staff') || i.path.includes('users'))).toBe(true);
    });

    it('returns cash register menu for cash_register.manage', () => {
      const items = getMenuItemsAffectedByPermission(AppPermissions.CashRegisterManage);
      expect(items.some((i) => i.path.includes('kassenverwaltung'))).toBe(true);
      expect(items.some((i) => i.icon != null)).toBe(true);
    });

    it('returns empty for blank permission', () => {
      expect(getMenuItemsAffectedByPermission('')).toEqual([]);
    });
  });

  describe('getMenuChipsForPermissionGroup', () => {
    it('returns IA menu chips for tagesabschluss group', () => {
      const chips = getMenuChipsForPermissionGroup('tagesabschluss');
      expect(chips.length).toBeGreaterThan(0);
      expect(chips.some((c) => c.key === 'tagesabschluss')).toBe(true);
      expect(chips[0]?.icon).toBeTruthy();
    });

    it('returns empty for unknown group', () => {
      expect(getMenuChipsForPermissionGroup('not-a-group')).toEqual([]);
    });
  });

  describe('getPermissionsAffectingMenu', () => {
    it('returns daily-closing gates for /tagesabschluss', () => {
      const reqs = getPermissionsAffectingMenu('/tagesabschluss');
      expect(reqs.some((r) => r.key === PERMISSIONS.DAILY_CLOSING_VIEW)).toBe(true);
      expect(reqs.some((r) => r.key === PERMISSIONS.DAILY_CLOSING_EXECUTE)).toBe(true);
    });

    it('returns empty for unknown menu', () => {
      expect(getPermissionsAffectingMenu('/definitely-missing')).toEqual([]);
    });
  });

  describe('listSidebarMenuFilterOptions', () => {
    it('includes primary catalog leaves', () => {
      const opts = listSidebarMenuFilterOptions();
      expect(opts.some((o) => o.value === '/tagesabschluss')).toBe(true);
      expect(opts.some((o) => o.value === '/tables')).toBe(true);
    });
  });
});
