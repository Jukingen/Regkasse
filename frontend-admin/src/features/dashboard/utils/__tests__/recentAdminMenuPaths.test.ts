import { beforeEach, describe, expect, it } from 'vitest';

import { filterAccessibleHandyToolHrefs } from '@/features/dashboard/utils/dashboardHandyTools';
import {
  MAX_RECENT_ADMIN_MENU_PATHS,
  RECENT_ADMIN_MENU_STORAGE_KEY,
  getSidebarLabelKeyForPath,
  readRecentAdminMenuPaths,
  rememberRecentAdminMenuPath,
  resolveRecentMenuStorageKey,
} from '@/features/dashboard/utils/recentAdminMenuPaths';

describe('recentAdminMenuPaths', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('maps nested tenant URLs to the catalog leaf', () => {
    expect(resolveRecentMenuStorageKey('/admin/tenants/abc-123')).toBe('/admin/tenants');
    expect(resolveRecentMenuStorageKey('/admin/cash-registers')).toBe('/admin/cash-registers');
    expect(resolveRecentMenuStorageKey('/kassenverwaltung')).toBe('/kassenverwaltung');
  });

  it('skips dashboard, auth, and 403', () => {
    expect(resolveRecentMenuStorageKey('/dashboard')).toBeNull();
    expect(resolveRecentMenuStorageKey('/login')).toBeNull();
    expect(resolveRecentMenuStorageKey('/403')).toBeNull();
    expect(resolveRecentMenuStorageKey('/force-password-change')).toBeNull();
  });

  it('keeps the last four unique paths, most recent first', () => {
    rememberRecentAdminMenuPath('/kassenverwaltung');
    rememberRecentAdminMenuPath('/receipts');
    rememberRecentAdminMenuPath('/tagesabschluss');
    rememberRecentAdminMenuPath('/shifts');
    rememberRecentAdminMenuPath('/backup');
    rememberRecentAdminMenuPath('/kassenverwaltung');

    const stored = readRecentAdminMenuPaths();
    expect(stored).toEqual(['/kassenverwaltung', '/backup', '/shifts', '/tagesabschluss']);
    expect(stored).toHaveLength(MAX_RECENT_ADMIN_MENU_PATHS);
    expect(JSON.parse(localStorage.getItem(RECENT_ADMIN_MENU_STORAGE_KEY) ?? '[]')).toEqual(stored);
  });

  it('resolves catalog label keys for stored leaves', () => {
    expect(getSidebarLabelKeyForPath('/admin/cash-registers')).toBe('nav.superAdminCashRegisters');
    expect(getSidebarLabelKeyForPath('/kassenverwaltung')).toBe('nav.cashRegisters');
  });
});

describe('filterAccessibleHandyToolHrefs', () => {
  it('keeps only paths the caller can access', () => {
    const allowed = new Set(['/kassenverwaltung', '/receipts']);
    expect(
      filterAccessibleHandyToolHrefs(['/admin/cash-registers', '/kassenverwaltung', '/receipts'], (href) =>
        allowed.has(href)
      )
    ).toEqual(['/kassenverwaltung', '/receipts']);
  });
});
