import { describe, expect, it } from 'vitest';
import { MENU_PERMISSION } from '@/shared/auth/menuPermissions';
import { getRequiredPermissionForPath } from '@/shared/auth/routePermissions';
import { ADMIN_SIDEBAR_GROUP_KEYS, ADMIN_SIDEBAR_GROUP_ROUTES } from '@/shared/adminSidebarNavigation';
import { SETTINGS_AREA_ROUTE_PATHS } from '@/shared/settingsAreaRoutes';
import { SIDEBAR_NAV_ITEM_CATALOG } from '@/shared/adminSidebarRegistry';

describe('settingsAreaRoutesAlignment', () => {
    it('lists every settings hub path under Einstellungen group routes', () => {
        const settings = ADMIN_SIDEBAR_GROUP_ROUTES[ADMIN_SIDEBAR_GROUP_KEYS.settings];
        for (const path of SETTINGS_AREA_ROUTE_PATHS) {
            expect(settings, 'settings group should include settings paths').toContain(path);
        }
    });

    it('matches sidebar catalog hrefs for settings leaves', () => {
        const catalogHrefs = new Set(
            Object.values(SIDEBAR_NAV_ITEM_CATALOG)
                .map((item) => item.href)
                .filter((href) => href.startsWith('/settings')),
        );
        for (const path of SETTINGS_AREA_ROUTE_PATHS) {
            expect(catalogHrefs.has(path), `SIDEBAR_NAV_ITEM_CATALOG href should include ${path}`).toBe(true);
        }
    });

    it('has MENU_PERMISSION and ROUTE_PERMISSIONS for each settings path', () => {
        for (const path of SETTINGS_AREA_ROUTE_PATHS) {
            expect(MENU_PERMISSION[path], `MENU_PERMISSION[${path}]`).toBeDefined();
            expect(getRequiredPermissionForPath(path), `route guard for ${path}`).toBeDefined();
        }
    });
});
