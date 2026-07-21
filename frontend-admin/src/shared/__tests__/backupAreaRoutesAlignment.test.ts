import { describe, expect, it } from 'vitest';

import {
  ADMIN_SIDEBAR_GROUP_KEYS,
  ADMIN_SIDEBAR_GROUP_ROUTES,
} from '@/shared/adminSidebarNavigation';
import { SIDEBAR_NAV_ITEM_CATALOG } from '@/shared/adminSidebarRegistry';
import { MENU_PERMISSION } from '@/shared/auth/menuPermissions';
import { getRequiredPermissionForPath } from '@/shared/auth/routePermissions';
import { BACKUP_AREA_ROUTE_PATHS } from '@/shared/backupAreaRoutes';

describe('backupAreaRoutesAlignment', () => {
  it('lists every backup hub path under backup group routes', () => {
    const backup = ADMIN_SIDEBAR_GROUP_ROUTES[ADMIN_SIDEBAR_GROUP_KEYS.backup];
    for (const path of BACKUP_AREA_ROUTE_PATHS) {
      expect(backup, 'backup group should include backup paths').toContain(path);
    }
  });

  it('matches sidebar catalog hrefs for backup leaves', () => {
    const catalogHrefs = new Set(
      Object.values(SIDEBAR_NAV_ITEM_CATALOG)
        .map((item) => item.href)
        .filter((href) => href.startsWith('/backup') || href.startsWith('/settings/backup'))
    );
    expect(catalogHrefs.has('/backup')).toBe(true);
    expect(catalogHrefs.has('/backup/runs')).toBe(true);
  });

  it('has MENU_PERMISSION and ROUTE_PERMISSIONS for each backup path', () => {
    for (const path of BACKUP_AREA_ROUTE_PATHS) {
      expect(MENU_PERMISSION[path], `MENU_PERMISSION[${path}]`).toBeDefined();
      expect(getRequiredPermissionForPath(path), `route guard for ${path}`).toBeDefined();
    }
  });
});
