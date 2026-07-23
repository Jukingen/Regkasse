import { describe, expect, it } from 'vitest';

import { SIDEBAR_NAV_ITEM_CATALOG } from '@/shared/adminSidebarRegistry';
import { validateMenuPermissionMappings } from '@/shared/auth/menuPermissionMappingValidation';
import { ROUTE_PERMISSIONS } from '@/shared/auth/routePermissions';

describe('validateMenuPermissionMappings', () => {
  it('reports no issues for the live SIDEBAR_NAV_ITEM_CATALOG', () => {
    const issues = validateMenuPermissionMappings(SIDEBAR_NAV_ITEM_CATALOG, ROUTE_PERMISSIONS);
    expect(issues, JSON.stringify(issues, null, 2)).toEqual([]);
  });

  it('flags visible leaves without catalog.permission', () => {
    const issues = validateMenuPermissionMappings(
      {
        broken: {
          id: 'broken',
          menuKey: '/dashboard',
          href: '/dashboard',
          labelKey: 'nav.overview',
        },
      },
      ROUTE_PERMISSIONS
    );
    expect(issues.some((i) => i.code === 'missing_catalog_permission')).toBe(true);
  });

  it('flags missing ROUTE_PERMISSIONS entries', () => {
    const issues = validateMenuPermissionMappings(
      {
        orphan: {
          id: 'orphan',
          menuKey: '/definitely-not-a-route',
          href: '/definitely-not-a-route',
          labelKey: 'nav.overview',
          permission: 'report.view',
        },
      },
      ROUTE_PERMISSIONS
    );
    expect(issues.some((i) => i.code === 'missing_route_permission')).toBe(true);
  });
});
