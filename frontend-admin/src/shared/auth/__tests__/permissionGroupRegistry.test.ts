import { describe, expect, it } from 'vitest';

import {
  MENU_AREA_TO_PERMISSION_GROUP,
  MENU_AREAS_WITHOUT_PERMISSION_GROUP,
  PERMISSION_GROUP_ORDER,
  PERMISSION_GROUPS,
  buildPermissionUiGroupsFromCatalog,
  listMenuAreasMissingPermissionGroup,
  resolvePermissionGroupSlugForPermissionKey,
} from '@/shared/auth/permissionGroupRegistry';
import type { MenuAreaKey } from '@/shared/auth/menuPermissionRegistry';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { PERMISSION_GROUP_ORDER as ORDER_FROM_CATALOG_UTIL } from '@/features/users/utils/permissionCatalogGroup';

describe('permissionGroupRegistry', () => {
  it('exposes every group in PERMISSION_GROUP_ORDER', () => {
    for (const key of PERMISSION_GROUP_ORDER) {
      expect(PERMISSION_GROUPS[key].key).toBe(key);
    }
  });

  it('keeps permissionCatalogGroup order in sync with registry', () => {
    expect([...ORDER_FROM_CATALOG_UTIL]).toEqual([...PERMISSION_GROUP_ORDER]);
  });

  it('maps canonical permission keys to catalog groups (backend-aligned)', () => {
    expect(resolvePermissionGroupSlugForPermissionKey(PERMISSIONS.USER_VIEW)).toBe('mitarbeiter');
    expect(resolvePermissionGroupSlugForPermissionKey(PERMISSIONS.ROLE_MANAGE)).toBe('mitarbeiter');
    expect(resolvePermissionGroupSlugForPermissionKey('cash_register.view')).toBe('kassenverwaltung');
    expect(resolvePermissionGroupSlugForPermissionKey(PERMISSIONS.SHIFT_VIEW)).toBe('kassenverwaltung');
    expect(resolvePermissionGroupSlugForPermissionKey(PERMISSIONS.TABLE_VIEW)).toBe(
      'bestellung_verkauf'
    );
    expect(resolvePermissionGroupSlugForPermissionKey(PERMISSIONS.PAYMENT_VIEW)).toBe('zahlung');
    expect(resolvePermissionGroupSlugForPermissionKey(PERMISSIONS.DAILY_CLOSING_VIEW)).toBe(
      'tagesabschluss'
    );
    expect(resolvePermissionGroupSlugForPermissionKey(PERMISSIONS.FINANZONLINE_MANAGE)).toBe(
      'rksv_finanzonline'
    );
    expect(resolvePermissionGroupSlugForPermissionKey(PERMISSIONS.PRODUCT_VIEW)).toBe(
      'sortiment_preise'
    );
    expect(resolvePermissionGroupSlugForPermissionKey(PERMISSIONS.INVENTORY_VIEW)).toBe('lager');
    expect(resolvePermissionGroupSlugForPermissionKey(PERMISSIONS.CUSTOMER_VIEW)).toBe(
      'kunden_vorteile'
    );
    expect(resolvePermissionGroupSlugForPermissionKey(PERMISSIONS.REPORT_VIEW)).toBe('audit_berichte');
    expect(resolvePermissionGroupSlugForPermissionKey(PERMISSIONS.BACKUP_MANAGE)).toBe(
      'backup_disaster_recovery'
    );
    expect(resolvePermissionGroupSlugForPermissionKey('settings.backup')).toBe(
      'backup_disaster_recovery'
    );
    expect(resolvePermissionGroupSlugForPermissionKey(PERMISSIONS.SETTINGS_VIEW)).toBe(
      'einstellungen'
    );
    expect(resolvePermissionGroupSlugForPermissionKey(PERMISSIONS.DIGITAL_VIEW)).toBe(
      'digitale_dienste'
    );
    expect(resolvePermissionGroupSlugForPermissionKey(PERMISSIONS.SYSTEM_CRITICAL)).toBe('system');
  });

  it('does not invent plural/legacy aliases in representative permissions', () => {
    const all = Object.values(PERMISSION_GROUPS).flatMap((g) => [...g.permissions]);
    for (const key of all) {
      expect(key.startsWith('users.')).toBe(false);
      expect(key.startsWith('cashregister.')).toBe(false);
      expect(key.startsWith('roles.')).toBe(false);
    }
  });

  it('maps every menu area except intentional hubs', () => {
    expect(listMenuAreasMissingPermissionGroup()).toEqual([]);
    for (const area of Object.keys(MENU_AREA_TO_PERMISSION_GROUP) as MenuAreaKey[]) {
      if (MENU_AREAS_WITHOUT_PERMISSION_GROUP.has(area)) {
        expect(MENU_AREA_TO_PERMISSION_GROUP[area]).toBeNull();
      } else {
        expect(MENU_AREA_TO_PERMISSION_GROUP[area]).toBeTruthy();
      }
    }
  });

  it('buildPermissionUiGroupsFromCatalog orders by registry', () => {
    const groups = buildPermissionUiGroupsFromCatalog([
      { key: PERMISSIONS.PRODUCT_VIEW, group: 'Sortiment & Preise' },
      { key: PERMISSIONS.USER_VIEW, group: 'Mitarbeiter' },
      { key: PERMISSIONS.PAYMENT_VIEW, group: 'Zahlung' },
    ]);
    expect(groups.map((g) => g.slug)).toEqual(['mitarbeiter', 'zahlung', 'sortiment_preise']);
    expect(groups[0]?.definition?.icon).toBe('TeamOutlined');
  });
});
