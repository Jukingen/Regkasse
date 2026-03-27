/**
 * Admin sidebar: route ↔ menu selected key resolution and non-RKSV group open-state hints.
 * Menu item keys stay equal to App Router paths (except RKSV hub landing — see below).
 */

import type { MenuProps } from 'antd';

/** RKSV landing URL vs menu leaf key (Orval / menu model use /rksv/operations as selected key). */
export const RKSV_HUB_PATH = '/rksv';
export const RKSV_HUB_MENU_LEAF_KEY = '/rksv/operations';

/**
 * Non-RKSV submenu keys (prefix `grp-` — not in MENU_PERMISSION; parents are shown only if a child remains after filtering).
 */
export const ADMIN_SIDEBAR_GROUP_KEYS = {
    kasseBelege: 'grp-kasse-belege',
    sortiment: 'grp-sortiment',
    kundenVorteile: 'grp-kunden-vorteile',
    verwaltung: 'grp-verwaltung',
    /** Nested under Verwaltung: /settings + /settings/payment-methods */
    settingsArea: 'grp-settings-area',
} as const;

/** Route prefixes per group — used to auto-open the matching submenu for nested routes (e.g. /receipts/[id]). */
export const ADMIN_SIDEBAR_GROUP_ROUTES: Record<string, readonly string[]> = {
    [ADMIN_SIDEBAR_GROUP_KEYS.kasseBelege]: [
        '/operations-center',
        '/receipts',
        '/payments',
        '/reporting',
        '/tagesabschluss',
        '/receipt-templates',
        '/receipt-generate',
        '/tables',
    ],
    [ADMIN_SIDEBAR_GROUP_KEYS.sortiment]: ['/products', '/modifier-groups', '/categories', '/inventory'],
    [ADMIN_SIDEBAR_GROUP_KEYS.kundenVorteile]: ['/customers', '/benefit-definitions', '/benefit-assignments'],
    [ADMIN_SIDEBAR_GROUP_KEYS.verwaltung]: ['/users', '/settings', '/settings/payment-methods'],
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
    const p = pathname ?? '';
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
    const p = pathname ?? '';
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
            if (key === '/rksv') return ctx.isMenuItemAllowed(key, ctx.permissions);
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
