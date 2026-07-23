import { describe, expect, it } from 'vitest';

import {
  MENU_PERMISSION_MAP,
  getAllMenuKeys,
  getMenusForPermission,
  getPermissionForMenu,
  listMenuPermissionMapRows,
  listUnwiredMenuPermissionMapKeys,
  validateMenuPermissions,
  type MenuPermissionMapKey,
} from '@/shared/auth/menuPermissionMapping';
import {
  getWiredSidebarMenuAreas,
  SIDEBAR_NAV_ITEM_CATALOG,
  validateSidebarMenuPermissionMappings,
} from '@/shared/adminSidebarRegistry';
import {
  MENU_AREA_TO_PERMISSION_GROUP,
  type PermissionGroupKey,
} from '@/shared/auth/permissionGroupRegistry';
import { resolveMenuAreaPermissions } from '@/shared/auth/menuPermissionRegistry';
import { PERMISSIONS } from '@/shared/auth/permissions';

/** Inventedish aliases that must never appear in this map. */
const FORBIDDEN_KEYS = [
  'dashboard.view',
  'users.view',
  'users.manage',
  'cashregister.view',
  'cashregister.manage',
  'rksv.view',
  'rksv.manage',
  'backup.view',
  'dailyclosing.view',
  'dailyclosing.execute',
  'sale.manage',
];

describe('MENU_PERMISSION_MAP', () => {
  it('has no inventedish permission key aliases', () => {
    const allKeys = listMenuPermissionMapRows().flatMap((row) => {
      const implied = row.impliedBy
        ? Array.isArray(row.impliedBy)
          ? [...row.impliedBy]
          : [row.impliedBy]
        : [];
      return [row.permissionKey, ...(row.permissionKeysAnyOf ?? []), ...implied].filter(Boolean);
    });
    for (const forbidden of FORBIDDEN_KEYS) {
      expect(allKeys, forbidden).not.toContain(forbidden);
    }
  });

  it('permissionGroup matches MENU_AREA_TO_PERMISSION_GROUP', () => {
    for (const row of listMenuPermissionMapRows()) {
      const expected = MENU_AREA_TO_PERMISSION_GROUP[row.menuKey];
      expect(row.permissionGroup as PermissionGroupKey | null).toBe(expected);
    }
  });

  it('primary permissionKey is covered by MENU_PERMISSIONS when area has a single gate', () => {
    const singleGateAreas: MenuPermissionMapKey[] = [
      'tables',
      'cashRegisters',
      'shifts',
      'sales',
      'tagesabschluss',
      'rksv',
      'products',
      'categories',
      'customers',
      'benefits',
      'reports',
      'backup',
      'settings',
      'workingHours',
      'tenants',
      'users',
      'license',
    ];
    for (const area of singleGateAreas) {
      const registry = resolveMenuAreaPermissions(area);
      const mapped = MENU_PERMISSION_MAP[area].permissionKey;
      expect(registry, area).toContain(mapped);
    }
  });
});

describe('getPermissionForMenu / getMenusForPermission', () => {
  it('resolves mapped menu keys and returns null for unknown keys', () => {
    expect(getPermissionForMenu('tables')).toBe(PERMISSIONS.TABLE_VIEW);
    expect(getPermissionForMenu('settings')).toBe(PERMISSIONS.SETTINGS_VIEW);
    expect(getPermissionForMenu('workingHours')).toBe(PERMISSIONS.SETTINGS_VIEW);
    expect(getPermissionForMenu('not-a-menu')).toBeNull();
  });

  it('lists all menus that share a primary permission key', () => {
    expect(getMenusForPermission(PERMISSIONS.SETTINGS_VIEW).sort()).toEqual(
      ['backup', 'settings', 'workingHours'].sort()
    );
    expect(getMenusForPermission(PERMISSIONS.TABLE_VIEW)).toEqual(['tables']);
    expect(getMenusForPermission('does.not.exist')).toEqual([]);
  });

  it('validateMenuPermissions flags unknown sidebar keys', () => {
    expect(validateMenuPermissions(['tables', 'ghost-area'])).toEqual(['ghost-area']);
    expect(validateMenuPermissions(getAllMenuKeys())).toEqual([]);
  });

  it('wires every MENU_PERMISSION_MAP key into the sidebar catalog', () => {
    expect(listUnwiredMenuPermissionMapKeys(getWiredSidebarMenuAreas())).toEqual([]);
    expect(validateSidebarMenuPermissionMappings()).toEqual([]);
    expect(SIDEBAR_NAV_ITEM_CATALOG.tagesabschluss.permission).toBe(
      PERMISSIONS.DAILY_CLOSING_VIEW
    );
    expect(SIDEBAR_NAV_ITEM_CATALOG.tagesabschluss.permissionGroup).toBe('tagesabschluss');
    expect(SIDEBAR_NAV_ITEM_CATALOG.tagesabschluss.menuArea).toBe('tagesabschluss');
  });
});
