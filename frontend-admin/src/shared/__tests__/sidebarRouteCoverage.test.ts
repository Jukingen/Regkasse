import { describe, expect, it } from 'vitest';

import { getAllSidebarLeafMenuKeysForCoverage } from '@/shared/adminSidebarRegistry';
import { ROUTE_GUARD_PATHS_WITHOUT_SIDEBAR_LEAF } from '@/shared/adminSidebarRouteRegistry';
import { MENU_PERMISSION } from '@/shared/auth/menuPermissions';
import { getRequiredPermissionForPath } from '@/shared/auth/routePermissions';
import { buildRksvMenuGroups } from '@/shared/rksvMenuModel';

const passthroughT = (key: string) => key;

describe('sidebarRouteCoverage', () => {
  it('maps every sidebar leaf (catalog + RKSV) to MENU_PERMISSION and ROUTE_PERMISSIONS', () => {
    const groups = buildRksvMenuGroups(passthroughT, 'verifications');
    const leaves = getAllSidebarLeafMenuKeysForCoverage(groups);
    for (const route of leaves) {
      expect(MENU_PERMISSION[route], `MENU_PERMISSION should define ${route}`).toBeDefined();
      expect(
        getRequiredPermissionForPath(route),
        `ROUTE_PERMISSIONS must cover ${route}`
      ).toBeDefined();
    }
  });

  it('documents routes that are guarded but not linked from the sidebar', () => {
    for (const route of ROUTE_GUARD_PATHS_WITHOUT_SIDEBAR_LEAF) {
      expect(getRequiredPermissionForPath(route)).toBeDefined();
    }
  });
});
