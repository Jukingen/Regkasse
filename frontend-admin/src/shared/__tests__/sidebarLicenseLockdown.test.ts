import { describe, expect, it } from 'vitest';

import type { MenuProps } from 'antd';

import { ADMIN_SIDEBAR_GROUP_KEYS } from '@/shared/adminSidebarNavigation';
import {
  SIDEBAR_LICENSE_LOCKDOWN_MENU_POLICY,
  filterSidebarMenuItemsForLicenseLockdown,
  shouldDisableSidebarKeyForLicenseLockdown,
  shouldHideSidebarKeyForLicenseLockdown,
} from '@/shared/sidebarLicenseLockdown';

describe('sidebarLicenseLockdown', () => {
  it('keeps license, dashboard, payments, backup when locked', () => {
    expect(shouldHideSidebarKeyForLicenseLockdown('/admin/license')).toBe(false);
    expect(shouldHideSidebarKeyForLicenseLockdown('/admin/license-management')).toBe(false);
    expect(shouldHideSidebarKeyForLicenseLockdown('/license/dashboard')).toBe(false);
    expect(shouldHideSidebarKeyForLicenseLockdown('/dashboard')).toBe(false);
    expect(shouldHideSidebarKeyForLicenseLockdown('/payments')).toBe(false);
    expect(shouldHideSidebarKeyForLicenseLockdown('/backup')).toBe(false);
    expect(shouldHideSidebarKeyForLicenseLockdown('/settings/data-management')).toBe(false);
    expect(shouldHideSidebarKeyForLicenseLockdown('/settings/account')).toBe(false);
  });

  it('hides write-oriented leaves when locked', () => {
    expect(shouldHideSidebarKeyForLicenseLockdown('/products')).toBe(true);
    expect(shouldHideSidebarKeyForLicenseLockdown('/admin/users')).toBe(true);
    expect(shouldHideSidebarKeyForLicenseLockdown('/rksv')).toBe(true);
    expect(shouldHideSidebarKeyForLicenseLockdown('/kassenverwaltung')).toBe(true);
    expect(shouldHideSidebarKeyForLicenseLockdown(ADMIN_SIDEBAR_GROUP_KEYS.catalog)).toBe(true);
    expect(shouldHideSidebarKeyForLicenseLockdown(ADMIN_SIDEBAR_GROUP_KEYS.rksv)).toBe(true);
  });

  it('disables tenants when locked', () => {
    expect(shouldDisableSidebarKeyForLicenseLockdown('/admin/tenants')).toBe(true);
    expect(shouldHideSidebarKeyForLicenseLockdown('/admin/tenants')).toBe(false);
  });

  it('filters menu tree and disables tenants', () => {
    const items: MenuProps['items'] = [
      { key: '/dashboard', label: 'Dashboard' },
      { key: '/products', label: 'Products' },
      { key: '/payments', label: 'Payments' },
      { key: '/admin/tenants', label: 'Tenants' },
      {
        key: ADMIN_SIDEBAR_GROUP_KEYS.catalog,
        label: 'Catalog',
        children: [{ key: '/products', label: 'Products' }],
      },
      {
        key: ADMIN_SIDEBAR_GROUP_KEYS.salesTransactions,
        label: 'Sales',
        children: [
          { key: '/payments', label: 'Payments' },
          { key: '/vouchers', label: 'Vouchers' },
        ],
      },
    ];

    const filtered = filterSidebarMenuItemsForLicenseLockdown(items, {
      licenseState: 'Locked',
      isSuperAdmin: false,
    });

    const keys = (filtered ?? [])
      .filter((item): item is { key?: string; disabled?: boolean; children?: MenuProps['items'] } =>
        Boolean(item && typeof item === 'object')
      )
      .map((item) => item.key);

    expect(keys).toContain('/dashboard');
    expect(keys).toContain('/payments');
    expect(keys).toContain('/admin/tenants');
    expect(keys).not.toContain('/products');
    expect(keys).not.toContain(ADMIN_SIDEBAR_GROUP_KEYS.catalog);

    const tenants = (filtered ?? []).find(
      (item) => item && typeof item === 'object' && 'key' in item && item.key === '/admin/tenants'
    ) as { disabled?: boolean } | undefined;
    expect(tenants?.disabled).toBe(true);

    const sales = (filtered ?? []).find(
      (item) =>
        item &&
        typeof item === 'object' &&
        'key' in item &&
        item.key === ADMIN_SIDEBAR_GROUP_KEYS.salesTransactions
    ) as { children?: MenuProps['items'] } | undefined;
    const salesKeys = (sales?.children ?? [])
      .filter((c): c is { key?: string } => Boolean(c && typeof c === 'object'))
      .map((c) => c.key);
    expect(salesKeys).toEqual(['/payments']);
  });

  it('skips filtering for Super Admin and Active/Grace', () => {
    const items: MenuProps['items'] = [{ key: '/products', label: 'Products' }];
    expect(
      filterSidebarMenuItemsForLicenseLockdown(items, {
        licenseState: 'Locked',
        isSuperAdmin: true,
      })
    ).toEqual(items);
    expect(
      filterSidebarMenuItemsForLicenseLockdown(items, {
        licenseState: 'Grace',
        isSuperAdmin: false,
      })
    ).toEqual(items);
  });

  it('exposes IA policy summary matching the lockdown sketch', () => {
    expect(SIDEBAR_LICENSE_LOCKDOWN_MENU_POLICY.license.visibleWhenLocked).toBe(true);
    expect(SIDEBAR_LICENSE_LOCKDOWN_MENU_POLICY.users.visibleWhenLocked).toBe(false);
    expect(SIDEBAR_LICENSE_LOCKDOWN_MENU_POLICY.products.visibleWhenLocked).toBe(false);
    expect(SIDEBAR_LICENSE_LOCKDOWN_MENU_POLICY.rksv.visibleWhenLocked).toBe(false);
    expect(SIDEBAR_LICENSE_LOCKDOWN_MENU_POLICY.payments.readOnlyWhenLocked).toBe(true);
    expect(SIDEBAR_LICENSE_LOCKDOWN_MENU_POLICY.tenants.disabledWhenLocked).toBe(true);
  });
});
