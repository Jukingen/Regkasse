import { describe, expect, it } from 'vitest';

import {
  MENU_AREA_PRIMARY_PATH,
  MENU_PERMISSIONS,
  canAccessMenuArea,
  getMenuPermissionState,
  resolveMenuAreaKey,
  resolveMenuAreaPermissions,
  tryRegistryMenuVisibility,
  type MenuAreaKey,
} from '@/shared/auth/menuPermissionRegistry';
import { AppPermissions, PERMISSIONS } from '@/shared/auth/permissions';

describe('menuPermissionRegistry', () => {
  it('exposes every menu area with a primary path', () => {
    const areas = Object.keys(MENU_PERMISSIONS) as MenuAreaKey[];
    expect(areas.length).toBeGreaterThan(0);
    for (const area of areas) {
      expect(MENU_AREA_PRIMARY_PATH[area], area).toMatch(/^\//);
    }
  });

  it('uses backend-aligned permission keys (no inventedish aliases)', () => {
    expect(resolveMenuAreaPermissions('tables')).toEqual([PERMISSIONS.TABLE_VIEW]);
    expect(resolveMenuAreaPermissions('cashRegisters')).toEqual([
      AppPermissions.CashRegisterManage,
    ]);
    expect(resolveMenuAreaPermissions('employees')).toEqual([
      PERMISSIONS.USER_VIEW,
      PERMISSIONS.REPORT_VIEW,
      PERMISSIONS.SHIFT_VIEW,
    ]);
    expect(resolveMenuAreaPermissions('users')).toEqual([PERMISSIONS.USER_VIEW]);
    expect(resolveMenuAreaPermissions('rksv')).toEqual([PERMISSIONS.FINANZONLINE_MANAGE]);
    expect(resolveMenuAreaPermissions('backup')).toEqual([PERMISSIONS.SETTINGS_VIEW]);
    expect(resolveMenuAreaPermissions('billing')).toEqual([PERMISSIONS.SYSTEM_CRITICAL]);
    expect(resolveMenuAreaPermissions('tagesabschluss')).toEqual([PERMISSIONS.DAILY_CLOSING_VIEW]);
    expect(resolveMenuAreaPermissions('dashboard')).toEqual([]);
    expect(MENU_PERMISSIONS.dashboard.fallback).toBe(true);
  });

  it('resolveMenuAreaKey accepts IA key and exact primary path only', () => {
    expect(resolveMenuAreaKey('tagesabschluss')).toBe('tagesabschluss');
    expect(resolveMenuAreaKey('/tagesabschluss')).toBe('tagesabschluss');
    expect(resolveMenuAreaKey('/tagesabschluss/execute')).toBeUndefined();
    expect(resolveMenuAreaKey('/unknown-route')).toBeUndefined();
  });

  it('canAccessMenuArea respects permission claims and fallback', () => {
    expect(canAccessMenuArea('tables', { permissions: [PERMISSIONS.TABLE_VIEW] })).toBe(true);
    expect(canAccessMenuArea('tables', { permissions: [PERMISSIONS.PRODUCT_VIEW] })).toBe(false);

    expect(canAccessMenuArea('dashboard', { permissions: [PERMISSIONS.PRODUCT_VIEW] })).toBe(true);
    expect(canAccessMenuArea('dashboard', { permissions: [] })).toBe(false);
    expect(canAccessMenuArea('dashboard', null)).toBe(false);

    expect(
      canAccessMenuArea('cashRegisters', { permissions: [AppPermissions.CashRegisterView] })
    ).toBe(false);
    expect(
      canAccessMenuArea('cashRegisters', { permissions: [AppPermissions.CashRegisterManage] })
    ).toBe(true);
  });

  it('getMenuPermissionState returns visible + primary permission (implication-aware)', () => {
    expect(
      getMenuPermissionState('tagesabschluss', { permissions: [PERMISSIONS.DAILY_CLOSING_VIEW] })
    ).toEqual({ visible: true, permission: PERMISSIONS.DAILY_CLOSING_VIEW });

    // execute → view implication (hasPermission)
    expect(
      getMenuPermissionState('tagesabschluss', {
        permissions: [PERMISSIONS.DAILY_CLOSING_EXECUTE],
      })
    ).toEqual({ visible: true, permission: PERMISSIONS.DAILY_CLOSING_VIEW });

    expect(
      getMenuPermissionState('tagesabschluss', { permissions: [PERMISSIONS.SALE_VIEW] })
    ).toEqual({ visible: false, permission: PERMISSIONS.DAILY_CLOSING_VIEW });

    expect(
      getMenuPermissionState('tagesabschluss', { permissions: [] }, { isSuperAdmin: true })
    ).toEqual({ visible: true, permission: PERMISSIONS.DAILY_CLOSING_VIEW });
  });

  it('tryRegistryMenuVisibility falls through for unmapped paths', () => {
    expect(
      tryRegistryMenuVisibility('/dashboard', { permissions: [PERMISSIONS.PRODUCT_VIEW] })
    ).toBe(true);
    expect(
      tryRegistryMenuVisibility('/reporting/staff', { permissions: [PERMISSIONS.REPORT_VIEW] })
    ).toBeUndefined();
    expect(
      tryRegistryMenuVisibility('/tagesabschluss', {
        permissions: [PERMISSIONS.DAILY_CLOSING_VIEW],
      })
    ).toBe(true);
  });
});
