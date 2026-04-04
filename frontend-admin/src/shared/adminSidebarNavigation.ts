/**
 * Admin sidebar: route ↔ menu selected key resolution and non-RKSV group open-state hints.
 * Menu item keys stay equal to App Router paths (except RKSV hub landing — see below).
 *
 * IA source of truth: `src/shared/adminSidebarRegistry.ts` + `src/shared/buildAdminSidebar.tsx`.
 * RKSV: `src/features/rksv/sidebarPlugin.ts` (`registerRksvSidebar`) + `rksvAdminMenuModel`.
 * Permission filtering: `menuPermissions.MENU_PERMISSION` + `filterSidebarMenuItems`.
 * Route protection: `routePermissions.ROUTE_PERMISSIONS` + `PermissionRouteGuard` (unchanged contract).
 *
 * Role fallback (when `usePermissionFirst` is false — no permissions on `/me` yet): only `/users` and
 * `/rksv` parent keys use `canViewUsers` / `canShowRksvMenu`; other leaves stay visible unless
 * permission mode is on. Prefer keeping `/me` permissions authoritative.
 */

import type { MenuProps } from 'antd';
import type { RksvMenuGroup } from '@/shared/rksvMenuModel';
import { getRksvOpenSubgroupKeys } from '@/shared/rksvMenuModel';
import { SETTINGS_AREA_ROUTE_PATHS } from '@/shared/settingsAreaRoutes';

/** RKSV landing URL vs menu leaf key (Orval / menu model use /rksv/operations as selected key). */
export const RKSV_HUB_PATH = '/rksv';
export const RKSV_HUB_MENU_LEAF_KEY = '/rksv/operations';

/**
 * Normalize pathname for menu matching (strip trailing slashes; `/` stays `/`).
 */
export function normalizeAdminPathname(pathname: string | null | undefined): string {
    if (pathname == null || pathname === '') return '';
    let p = pathname.replace(/\/+$/, '');
    if (p === '') p = '/';
    return p;
}

/**
 * Non-RKSV submenu keys (prefix `grp-` — not in MENU_PERMISSION; parents are shown only if a child remains after filtering).
 */
export const ADMIN_SIDEBAR_GROUP_KEYS = {
    operations: 'grp-operations',
    salesTransactions: 'grp-sales-transactions',
    catalogPricing: 'grp-catalog-pricing',
    customersBenefits: 'grp-customers-benefits',
    reportingAnalytics: 'grp-reporting-analytics',
    reportingFormal: 'grp-reporting-formal',
    fiscalCompliance: 'grp-fiscal-compliance',
    verwaltung: 'grp-verwaltung',
    /** Nested under Verwaltung: /settings + /settings/payment-methods */
    settingsArea: 'grp-settings-area',
} as const;

/** Route prefixes per group — used to auto-open the matching submenu for nested routes (e.g. /receipts/[id]). */
export const ADMIN_SIDEBAR_GROUP_ROUTES: Record<string, readonly string[]> = {
    [ADMIN_SIDEBAR_GROUP_KEYS.operations]: ['/operations-center', '/tables', '/tagesabschluss'],
    [ADMIN_SIDEBAR_GROUP_KEYS.salesTransactions]: [
        '/receipts',
        '/payments',
        '/invoices',
        '/receipt-templates',
        '/receipt-generate',
    ],
    [ADMIN_SIDEBAR_GROUP_KEYS.catalogPricing]: ['/products', '/modifier-groups', '/categories', '/inventory', '/pricing-rules'],
    [ADMIN_SIDEBAR_GROUP_KEYS.customersBenefits]: ['/customers', '/benefit-definitions', '/benefit-assignments'],
    [ADMIN_SIDEBAR_GROUP_KEYS.reportingAnalytics]: [
        '/dashboard',
        '/reporting',
        '/reporting/report-center',
        '/reporting/staff',
        '/reporting/tagesbericht',
        '/reporting/monatsbericht',
        '/reporting/jahresbericht',
    ],
    [ADMIN_SIDEBAR_GROUP_KEYS.reportingFormal]: [
        '/reporting/tagesbericht',
        '/reporting/monatsbericht',
        '/reporting/jahresbericht',
    ],
    [ADMIN_SIDEBAR_GROUP_KEYS.fiscalCompliance]: ['/audit-logs', '/rksv'],
    [ADMIN_SIDEBAR_GROUP_KEYS.verwaltung]: ['/users', ...SETTINGS_AREA_ROUTE_PATHS],
};

/**
 * Collect leaf menu keys (routes) from built Ant Design menu items — excludes submenu group keys.
 */
export function collectSelectableRouteKeysFromMenuItems(items: MenuProps['items'] | undefined): string[] {
    const out: string[] = [];
    const walk = (list: MenuProps['items']) => {
        if (!list) return;
        for (const it of list) {
            if (!it || typeof it !== 'object') continue;
            if ('type' in it && it.type === 'divider') continue;
            const node = it as { key?: string; children?: MenuProps['items'] };
            if (node.children && node.children.length > 0) {
                walk(node.children);
                continue;
            }
            if (node.key && typeof node.key === 'string') out.push(node.key);
        }
    };
    walk(items);
    return out;
}

/**
 * Selected menu key(s): RKSV hub alias; otherwise longest matching leaf prefix for detail routes.
 */
export function resolveAdminMenuSelectedKeys(
    pathname: string | null | undefined,
    selectableLeafKeys: readonly string[],
): string[] {
    const p = normalizeAdminPathname(pathname);
    if (!p) return [];
    if (p === RKSV_HUB_PATH) return [RKSV_HUB_MENU_LEAF_KEY];

    const sorted = [...selectableLeafKeys].sort((a, b) => b.length - a.length);
    for (const key of sorted) {
        if (p === key || p.startsWith(`${key}/`)) return [key];
    }
    return [p];
}

/** Subgroup keys to open when pathname falls under a non-RKSV route group. */
export function getNonRksvSidebarOpenGroupKeys(pathname: string | null | undefined): string[] {
    const p = normalizeAdminPathname(pathname);
    const keys: string[] = [];
    for (const [groupKey, routes] of Object.entries(ADMIN_SIDEBAR_GROUP_ROUTES)) {
        if (routes.some((r) => p === r || p.startsWith(`${r}/`))) keys.push(groupKey);
    }
    if (p === '/settings' || p.startsWith('/settings/')) {
        keys.push(ADMIN_SIDEBAR_GROUP_KEYS.settingsArea);
    }
    return keys;
}

export type SidebarPermissionContext = {
    usePermissionFirst: boolean;
    permissions: string[];
    userRole: string;
    isMenuItemAllowed: (key: string, permissions: string[] | undefined) => boolean;
    canViewUsers: (role: string) => boolean;
    canShowRksvMenu: (role: string) => boolean;
};

/**
 * Recursively filter menu items by permission / role. Submenus with no visible children are removed.
 */
export function filterSidebarMenuItems(
    items: MenuProps['items'] | undefined,
    ctx: SidebarPermissionContext,
): MenuProps['items'] {
    if (!items?.length) return items;

    const leafAllowed = (key: string): boolean => {
        if (ctx.usePermissionFirst) {
            return ctx.isMenuItemAllowed(key, ctx.permissions);
        }
        if (key === '/users') return ctx.canViewUsers(ctx.userRole);
        if (key === '/rksv') return ctx.canShowRksvMenu(ctx.userRole);
        return true;
    };

    const result: MenuProps['items'] = [];

    for (const it of items) {
        if (!it || typeof it !== 'object') continue;
        if ('type' in it && it.type === 'divider') {
            result.push(it);
            continue;
        }
        const node = it as {
            key?: string;
            children?: MenuProps['items'];
            [k: string]: unknown;
        };

        if (node.children && node.children.length > 0) {
            const parentKey = node.key;
            if (parentKey && typeof parentKey === 'string' && !leafAllowed(parentKey)) continue;
            const filteredChildren = filterSidebarMenuItems(node.children, ctx);
            if (!filteredChildren?.length) continue;
            result.push({ ...it, children: filteredChildren } as NonNullable<MenuProps['items']>[number]);
            continue;
        }

        const key = node.key;
        if (key && typeof key === 'string') {
            if (!leafAllowed(key)) continue;
        }
        result.push(it);
    }

    return result;
}

export type SidebarOpenKeysMergeParams = {
    pathname: string | null | undefined;
    /** Current open keys from React state (includes user-expanded submenus). */
    prevOpenKeys: readonly string[];
    canSeeRksv: boolean;
    rksvGroups: RksvMenuGroup[];
};

/**
 * Derives next `openKeys` after a navigation or permission change.
 * - Prunes stale `/rksv` and `rksv-grp-*` when the route leaves `/rksv/*` (avoids Ant Design inline menu drift).
 * - Closes the fiscal top group when neither audit nor RKSV routes are active.
 * - Merges route-driven auto-open hints; preserves unrelated user-opened keys.
 */
export function computeSidebarOpenKeysMerge(params: SidebarOpenKeysMergeParams): string[] {
    const p = normalizeAdminPathname(params.pathname);
    const keys = new Set(params.prevOpenKeys);

    /** Drop RKSV submenu keys when the route is outside `/rksv` or the user cannot see RKSV at all. */
    if (!params.canSeeRksv || !p.startsWith('/rksv')) {
        for (const k of [...keys]) {
            if (k === '/rksv' || k.startsWith('rksv-grp-')) {
                keys.delete(k);
            }
        }
    }

    /** Keep fiscal section open only on audit routes or on `/rksv/*` when the user may see RKSV. */
    const fiscalActive =
        p === '/audit-logs' ||
        p.startsWith('/audit-logs/') ||
        (params.canSeeRksv && p.startsWith('/rksv'));
    if (!fiscalActive) {
        keys.delete(ADMIN_SIDEBAR_GROUP_KEYS.fiscalCompliance);
    }

    for (const k of getNonRksvSidebarOpenGroupKeys(p)) {
        keys.add(k);
    }

    if (!params.canSeeRksv && p.startsWith('/rksv')) {
        keys.delete(ADMIN_SIDEBAR_GROUP_KEYS.fiscalCompliance);
    }

    if (params.canSeeRksv && p.startsWith('/rksv')) {
        keys.add(ADMIN_SIDEBAR_GROUP_KEYS.fiscalCompliance);
        keys.add('/rksv');
        for (const k of getRksvOpenSubgroupKeys(p, params.rksvGroups)) {
            keys.add(k);
        }
    }

    return Array.from(keys);
}
