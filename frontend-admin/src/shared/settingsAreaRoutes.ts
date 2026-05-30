/**
 * Settings hub: canonical App Router paths for the Verwaltung settings subtree.
 *
 * Ownership:
 * - Sidebar IA: `SIDEBAR_NAV_ITEM_CATALOG` (`adminSidebarRegistry.ts`) must list the same hrefs.
 * - Horizontal nav: `SettingsSecondaryNav` builds tabs from this list + `ADMIN_NAV_LABEL_KEYS`.
 * - Access: `MENU_PERMISSION` + `ROUTE_PERMISSIONS` (see `settingsAreaRoutesAlignment` test).
 * - Group open-state: `ADMIN_SIDEBAR_GROUP_ROUTES[grp-verwaltung]` includes these after `/users`.
 */
export const SETTINGS_AREA_ROUTE_PATHS = [
    '/settings',
    '/settings/session',
    '/settings/personalization',
    '/settings/payment-methods',
    '/settings/backup-dr',
    '/settings/development-mode',
] as const;

export type SettingsAreaRoutePath = (typeof SETTINGS_AREA_ROUTE_PATHS)[number];
