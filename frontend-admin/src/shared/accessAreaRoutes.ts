/**
 * Access & roles hub: canonical App Router paths for secondary nav.
 * Sidebar IA: nested `grp-access` under Verwaltung (`adminSidebarRegistry.ts`).
 */
export const ACCESS_AREA_ROUTE_PATHS = [
    '/admin/access',
    '/admin/users',
    '/admin/access/roles',
    '/admin/access/matrix',
] as const;

export type AccessAreaRoutePath = (typeof ACCESS_AREA_ROUTE_PATHS)[number];

export const ACCESS_HUB_LANDING_PATH = '/admin/access' as const;
