import { describe, expect, it } from 'vitest';

import {
  LICENSE_LOCKDOWN_MENU_POLICY,
  LOCKDOWN_CORE_VISIBLE_PATHS,
  LOCKDOWN_HIDDEN_MENUS,
  LOCKDOWN_MENU_ALIAS_TO_KEYS,
  LOCKDOWN_READONLY_EXTRA_MENUS,
  LOCKDOWN_VISIBLE_MENUS,
} from '@/shared/licenseMenuConfig';
import { shouldHideSidebarKeyForLicenseLockdown } from '@/shared/sidebarLicenseLockdown';

describe('licenseMenuConfig', () => {
  it('defines core IA menus with real FA paths (no sketch placeholders)', () => {
    const keys = LOCKDOWN_VISIBLE_MENUS.map((m) => m.key);
    expect(keys).toEqual([
      'dashboard',
      'license',
      'settings',
      'profile',
      'data-export',
      'account-closure',
      'support',
    ]);
    expect(LOCKDOWN_VISIBLE_MENUS.find((m) => m.key === 'data-export')?.path).toBe(
      '/settings/data-management'
    );
    expect(LOCKDOWN_VISIBLE_MENUS.find((m) => m.key === 'account-closure')?.path).toBe(
      '/settings/account'
    );
    expect(LOCKDOWN_VISIBLE_MENUS.find((m) => m.key === 'profile')?.path).toBe(
      '/settings/password'
    );
  });

  it('keeps payments/backup/reports as read-only extras (not in hidden list)', () => {
    const readonlyKeys = LOCKDOWN_READONLY_EXTRA_MENUS.map((m) => m.key);
    expect(readonlyKeys).toContain('payments');
    expect(readonlyKeys).toContain('backup');
    expect(readonlyKeys).toContain('reports');
    expect(LOCKDOWN_HIDDEN_MENUS).not.toContain('payments');
    expect(LOCKDOWN_HIDDEN_MENUS).not.toContain('backup');
  });

  it('maps aliases to sidebar keys that stay visible under lockdown', () => {
    for (const entry of LOCKDOWN_VISIBLE_MENUS) {
      const keys = LOCKDOWN_MENU_ALIAS_TO_KEYS[entry.key];
      expect(keys.some((k) => !shouldHideSidebarKeyForLicenseLockdown(k))).toBe(true);
    }
    for (const path of LOCKDOWN_CORE_VISIBLE_PATHS) {
      expect(shouldHideSidebarKeyForLicenseLockdown(path)).toBe(false);
    }
  });

  it('documents policy flags used by sidebar tests', () => {
    expect(LICENSE_LOCKDOWN_MENU_POLICY.license.visibleWhenLocked).toBe(true);
    expect(LICENSE_LOCKDOWN_MENU_POLICY.users.visibleWhenLocked).toBe(false);
    expect(LICENSE_LOCKDOWN_MENU_POLICY.tenants.disabledWhenLocked).toBe(true);
    expect(LICENSE_LOCKDOWN_MENU_POLICY.payments.readOnlyWhenLocked).toBe(true);
  });
});
