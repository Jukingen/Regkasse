import { describe, expect, it } from 'vitest';

import {
  isLicenseLockdownActionAllowed,
  isLicenseLockdownMenuVisible,
  LICENSE_LOCKDOWN_VISIBLE_MENU_ALIASES,
} from '@/hooks/useLicenseMenuVisibility';

describe('useLicenseMenuVisibility helpers', () => {
  it('exposes sketch-friendly visible menu aliases', () => {
    expect(LICENSE_LOCKDOWN_VISIBLE_MENU_ALIASES).toContain('dashboard');
    expect(LICENSE_LOCKDOWN_VISIBLE_MENU_ALIASES).toContain('license');
    expect(LICENSE_LOCKDOWN_VISIBLE_MENU_ALIASES).toContain('account-closure');
  });

  it('shows all menus when not locked', () => {
    expect(isLicenseLockdownMenuVisible('/products', { isLocked: false })).toBe(true);
    expect(isLicenseLockdownMenuVisible('catalog', { isLocked: false })).toBe(true);
  });

  it('keeps lockdown allowlist aliases visible when locked', () => {
    expect(isLicenseLockdownMenuVisible('dashboard', { isLocked: true })).toBe(true);
    expect(isLicenseLockdownMenuVisible('license', { isLocked: true })).toBe(true);
    expect(isLicenseLockdownMenuVisible('settings', { isLocked: true })).toBe(true);
    expect(isLicenseLockdownMenuVisible('data-export', { isLocked: true })).toBe(true);
    expect(isLicenseLockdownMenuVisible('account-closure', { isLocked: true })).toBe(true);
    expect(isLicenseLockdownMenuVisible('/settings/account', { isLocked: true })).toBe(true);
    expect(isLicenseLockdownMenuVisible('/payments', { isLocked: true })).toBe(true);
  });

  it('hides write-oriented menus when locked', () => {
    expect(isLicenseLockdownMenuVisible('/products', { isLocked: true })).toBe(false);
    expect(isLicenseLockdownMenuVisible('/rksv', { isLocked: true })).toBe(false);
    expect(isLicenseLockdownMenuVisible('/admin/users', { isLocked: true })).toBe(false);
  });

  it('Super Admin bypasses menu hide', () => {
    expect(
      isLicenseLockdownMenuVisible('/products', { isLocked: true, isSuperAdmin: true })
    ).toBe(true);
  });

  it('allows read and renewal actions when locked', () => {
    expect(isLicenseLockdownActionAllowed('viewProducts', { isLocked: true })).toBe(true);
    expect(isLicenseLockdownActionAllowed('read-reports', { isLocked: true })).toBe(true);
    expect(isLicenseLockdownActionAllowed('license-renew', { isLocked: true })).toBe(true);
    expect(isLicenseLockdownActionAllowed('data-export', { isLocked: true })).toBe(true);
    expect(isLicenseLockdownActionAllowed('account-closure', { isLocked: true })).toBe(true);
  });

  it('blocks write actions when locked', () => {
    expect(isLicenseLockdownActionAllowed('createProduct', { isLocked: true })).toBe(false);
    expect(isLicenseLockdownActionAllowed('delete-user', { isLocked: true })).toBe(false);
  });

  it('allows all actions when not locked', () => {
    expect(isLicenseLockdownActionAllowed('createProduct', { isLocked: false })).toBe(true);
  });
});
