/**
 * Admin sidebar: route ↔ menu selected key resolution and non-RKSV group open-state hints.
 * Menu item keys stay equal to App Router paths (except RKSV hub landing — see below).
 *
 * IA source of truth: `src/shared/adminSidebarRegistry.ts` + `src/shared/buildAdminSidebar.tsx`.
 * RKSV: `src/features/rksv/sidebarPlugin.ts` (`registerRksvSidebar`) + `rksvAdminMenuModel`.
 * Permission filtering: `menuPermissions.MENU_PERMISSION` + `filterSidebarMenuItems`.
 * Route protection: `routePermissions.ROUTE_PERMISSIONS` + `PermissionRouteGuard` (unchanged contract).
 *
 * When JWT has no permission claims (`usePermissionFirst` is false):
 * - If `ALLOW_EMPTY_PERMISSIONS_FOR_ROUTE_ACCESS` is false, hide all routable leaves (matches
 *   `PermissionRouteGuard` fail-closed). Submenu group keys `grp-*` / `rksv-grp-*` stay structural
 *   so children can be filtered to empty and removed.
 * - If the env flag is true (migration), legacy role fallbacks apply: `/users` uses `canViewUsers`,
 *   RKSV leaves use `isRksvRouteKeyAllowed`, other leaves stay visible. Prefer `/me` permissions in prod.
 */
import type { MenuProps } from 'antd';

import { isRksvRouteKeyAllowed } from '@/shared/auth/menuPermissions';
import { ALLOW_EMPTY_PERMISSIONS_FOR_ROUTE_ACCESS } from '@/shared/auth/routeGuardConfig';
import { BACKUP_AREA_ROUTE_PATHS } from '@/shared/backupAreaRoutes';
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
  dashboard: 'grp-dashboard',
  /** Super Admin: license management + billing sales; tenant license leaf when permitted. */
  license: 'grp-license',
  operations: 'grp-operations',
  /** Nested under Betrieb: receipts, payments, … */
  salesTransactions: 'grp-sales-transactions',
  rksv: 'grp-rksv',
  /** Nested under RKSV: Sonderbelege leaves */
  specialReceipts: 'grp-special-receipts',
  catalog: 'grp-catalog',
  customers: 'grp-customers',
  reports: 'grp-reports',
  settings: 'grp-settings',
  /** Verwaltung: Zugriff & Rollen + Mandanten (platform) */
  admin: 'grp-admin',
  /** Nested under Verwaltung: Zugriff & Rollen hub */
  accessArea: 'grp-access',
  /** Backup & Disaster Recovery */
  backup: 'grp-backup',
  /** Nested under Backup: schedule vs platform settings */
  backupConfig: 'grp-backup-config',
  /** Nested under Einstellungen: website / digital portal / customer portal */
  digitalServices: 'grp-digital-services',
  /** Nested under Lizenzverwaltung: Super Admin digital manage + requests */
  digitalAdmin: 'grp-digital-admin',
  /** @deprecated Use `settings` — kept for open-keys merge during migration */
  settingsArea: 'grp-settings',
  /** @deprecated Use `rksv` */
  fiscalCompliance: 'grp-rksv',
  /** @deprecated Use `specialReceipts` */
  fiscalRksvClosing: 'grp-special-receipts',
  /** @deprecated Use `reports` */
  reportingAnalytics: 'grp-reports',
  /** @deprecated Use `catalog` */
  catalogPricing: 'grp-catalog',
  /** @deprecated Use `customers` */
  customersBenefits: 'grp-customers',
  /** @deprecated Use `admin` */
  verwaltung: 'grp-admin',
} as const;

/** Route prefixes per group — used to auto-open the matching submenu for nested routes (e.g. /receipts/[id]). */
export const ADMIN_SIDEBAR_GROUP_ROUTES: Record<string, readonly string[]> = {
  [ADMIN_SIDEBAR_GROUP_KEYS.dashboard]: ['/dashboard'],
  [ADMIN_SIDEBAR_GROUP_KEYS.license]: [
    '/admin/license',
    '/admin/licenses',
    '/admin/billing',
    '/admin/billing/sales',
    '/admin/billing/stats',
    '/billing/digital',
    '/admin/digital',
  ],
  [ADMIN_SIDEBAR_GROUP_KEYS.digitalAdmin]: ['/admin/digital', '/admin/digital/requests'],
  [ADMIN_SIDEBAR_GROUP_KEYS.operations]: [
    '/operations-center',
    '/tables',
    '/kassenverwaltung',
    '/staff',
    '/staff/list',
    '/staff/performance',
    '/staff/shifts',
    '/shifts',
    '/tagesabschluss',
  ],
  [ADMIN_SIDEBAR_GROUP_KEYS.salesTransactions]: [
    '/receipts',
    '/orders',
    '/orders/online',
    '/payments',
    '/payments/trends',
    '/payments/storno-refund-audit',
    '/admin/payments/card-transactions',
    '/vouchers',
    '/invoices',
    '/receipt-templates',
    '/receipt-generate',
  ],
  [ADMIN_SIDEBAR_GROUP_KEYS.rksv]: [
    '/audit-logs',
    '/admin/audit/fiscal-exports',
    '/admin/tse',
    '/admin/rksv',
    '/rksv',
    '/reporting/tagesbericht',
    '/reporting/monatsbericht',
    '/reporting/jahresbericht',
  ],
  [ADMIN_SIDEBAR_GROUP_KEYS.specialReceipts]: [
    '/rksv/sb/startbeleg',
    '/rksv/sb/monatsbeleg',
    '/rksv/sb/jahresbeleg',
    '/rksv/sb/nullbeleg',
    '/rksv/sb/schlussbeleg',
    '/rksv/sb/test-helper',
    '/rksv/sonderbelege',
  ],
  [ADMIN_SIDEBAR_GROUP_KEYS.catalog]: [
    '/products',
    '/modifier-groups',
    '/categories',
    '/inventory',
    '/pricing-rules',
  ],
  [ADMIN_SIDEBAR_GROUP_KEYS.customers]: [
    '/customers',
    '/benefit-definitions',
    '/benefit-assignments',
  ],
  [ADMIN_SIDEBAR_GROUP_KEYS.reports]: [
    '/reporting',
    '/reporting/report-center',
    '/reporting/compliance',
    '/reporting/staff',
    '/audit-logs/activity',
    '/reports/daily-closing',
    '/admin/reports',
    '/admin/reports/user-activity',
  ],
  [ADMIN_SIDEBAR_GROUP_KEYS.settings]: [
    '/settings',
    ...SETTINGS_AREA_ROUTE_PATHS,
    '/settings/website',
    '/settings/digital',
    '/digital/customer-portal',
    '/admin/system/time-sync',
  ],
  [ADMIN_SIDEBAR_GROUP_KEYS.digitalServices]: [
    '/settings/website',
    '/settings/digital',
    '/digital/customer-portal',
  ],
  [ADMIN_SIDEBAR_GROUP_KEYS.backup]: [...BACKUP_AREA_ROUTE_PATHS],
  [ADMIN_SIDEBAR_GROUP_KEYS.admin]: [
    '/admin/access',
    '/admin/users',
    '/admin/access/roles',
    '/admin/access/matrix',
    '/admin/tenants',
    '/admin/approvals',
    '/admin/maintenance',
    '/admin/errors',
    '/admin/monitoring',
    '/admin/risk-dashboard',
    '/admin/tse-management',
    '/admin/tse/failover',
  ],
  [ADMIN_SIDEBAR_GROUP_KEYS.accessArea]: [
    '/admin/access',
    '/admin/users',
    '/admin/access/roles',
    '/admin/access/matrix',
  ],
};

/**
 * Collect leaf menu keys (routes) from built Ant Design menu items — excludes submenu group keys.
 */
export function collectSelectableRouteKeysFromMenuItems(
  items: MenuProps['items'] | undefined
): string[] {
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
/** Optional `search` without leading `?` (e.g. `focus=startbeleg&x=1`) — used for virtual Sonderbeleg sidebar keys. */
export function resolveAdminMenuSelectedKeys(
  pathname: string | null | undefined,
  selectableLeafKeys: readonly string[],
  search?: string
): string[] {
  const p = normalizeAdminPathname(pathname);
  if (!p) return [];
  if (p === RKSV_HUB_PATH) return [RKSV_HUB_MENU_LEAF_KEY];

  if (p === '/rksv/sonderbelege' && search && search.length > 0) {
    const sp = new globalThis.URLSearchParams(search);
    const focus = sp.get('focus')?.trim();
    const keys = selectableLeafKeys as string[];
    if (focus === 'startbeleg' && keys.includes('/rksv/sb/startbeleg')) {
      return ['/rksv/sb/startbeleg'];
    }
    if (focus === 'schlussbeleg' && keys.includes('/rksv/sb/schlussbeleg')) {
      return ['/rksv/sb/schlussbeleg'];
    }
    if (focus === 'monatsbeleg' && keys.includes('/rksv/sb/monatsbeleg')) {
      return ['/rksv/sb/monatsbeleg'];
    }
    if (focus === 'jahresbeleg' && keys.includes('/rksv/sb/jahresbeleg')) {
      return ['/rksv/sb/jahresbeleg'];
    }
    if (focus === 'nullbeleg' && keys.includes('/rksv/sb/nullbeleg')) {
      return ['/rksv/sb/nullbeleg'];
    }
    if (focus === 'test-helper' && keys.includes('/rksv/sb/test-helper')) {
      return ['/rksv/sb/test-helper'];
    }
  }

  if (p === '/backup/configuration' || p.startsWith('/backup/configuration/')) {
    const keys = selectableLeafKeys as string[];
    if (keys.includes('/backup/configuration/schedule')) {
      return ['/backup/configuration/schedule'];
    }
    if (keys.includes('/backup/configuration/platform')) {
      return ['/backup/configuration/platform'];
    }
  }

  if (p === '/backup/audit' && (selectableLeafKeys as string[]).includes('/backup/audit')) {
    return ['/backup/audit'];
  }

  const sorted = [...selectableLeafKeys].sort((a, b) => b.length - a.length);
  for (const key of sorted) {
    if (p === key || p.startsWith(`${key}/`)) return [key];
  }
  return [p];
}

/** True when pathname is under Einstellungen or Verwaltung sidebar groups (excludes backup hub). */
export function isVerwaltungAdminPath(pathname: string | null | undefined): boolean {
  const p = normalizeAdminPathname(pathname);
  if (BACKUP_AREA_ROUTE_PATHS.some((r) => p === r || p.startsWith(`${r}/`))) {
    return false;
  }
  const settingsRoutes = ADMIN_SIDEBAR_GROUP_ROUTES[ADMIN_SIDEBAR_GROUP_KEYS.settings];
  const adminRoutes = ADMIN_SIDEBAR_GROUP_ROUTES[ADMIN_SIDEBAR_GROUP_KEYS.admin];
  return (
    settingsRoutes.some((r) => p === r || p.startsWith(`${r}/`)) ||
    adminRoutes.some((r) => p === r || p.startsWith(`${r}/`))
  );
}

/** Subgroup keys to open when pathname falls under a non-RKSV route group. */
export function getNonRksvSidebarOpenGroupKeys(pathname: string | null | undefined): string[] {
  const p = normalizeAdminPathname(pathname);
  const keys: string[] = [];
  for (const [groupKey, routes] of Object.entries(ADMIN_SIDEBAR_GROUP_ROUTES)) {
    if (routes.some((r) => p === r || p.startsWith(`${r}/`))) keys.push(groupKey);
  }
  if (p === '/settings' || p.startsWith('/settings/') || p === '/admin/system/time-sync') {
    keys.push(ADMIN_SIDEBAR_GROUP_KEYS.settings);
  }
  if (p === '/backup/configuration' || p.startsWith('/backup/configuration/')) {
    keys.push(ADMIN_SIDEBAR_GROUP_KEYS.backupConfig);
  }
  if (
    p === '/admin/access' ||
    p.startsWith('/admin/access/') ||
    p === '/admin/users' ||
    p.startsWith('/admin/users/') ||
    p === '/admin/tenants' ||
    p.startsWith('/admin/tenants/') ||
    p === '/admin/data-management' ||
    p === '/admin/errors' ||
    p === '/admin/monitoring' ||
    p === '/admin/risk-dashboard' ||
    p === '/admin/tse-management' ||
    p === '/admin/tse/failover' ||
    p.startsWith('/admin/tse/failover/')
  ) {
    keys.push(ADMIN_SIDEBAR_GROUP_KEYS.admin);
  }
  if (
    p === '/admin/access' ||
    p.startsWith('/admin/access/') ||
    p === '/admin/users' ||
    p.startsWith('/admin/users/')
  ) {
    keys.push(ADMIN_SIDEBAR_GROUP_KEYS.accessArea);
  }
  if (
    p === '/admin/license' ||
    p === '/admin/licenses' ||
    p === '/admin/billing' ||
    p.startsWith('/admin/billing/') ||
    p === '/billing/digital' ||
    p.startsWith('/billing/digital/') ||
    p === '/admin/digital' ||
    p.startsWith('/admin/digital/')
  ) {
    keys.push(ADMIN_SIDEBAR_GROUP_KEYS.license);
  }
  if (p === '/admin/digital' || p.startsWith('/admin/digital/')) {
    keys.push(ADMIN_SIDEBAR_GROUP_KEYS.digitalAdmin);
  }
  if (
    p === '/settings/website' ||
    p.startsWith('/settings/website/') ||
    p === '/settings/digital' ||
    p.startsWith('/settings/digital/') ||
    p === '/digital/customer-portal' ||
    p.startsWith('/digital/customer-portal/')
  ) {
    keys.push(ADMIN_SIDEBAR_GROUP_KEYS.settings);
    keys.push(ADMIN_SIDEBAR_GROUP_KEYS.digitalServices);
  }
  if (p === '/tagesabschluss' || p.startsWith('/tagesabschluss/')) {
    keys.push(ADMIN_SIDEBAR_GROUP_KEYS.operations);
  }
  if (
    p === '/receipts' ||
    p.startsWith('/receipts/') ||
    p === '/orders' ||
    p.startsWith('/orders/') ||
    p === '/payments' ||
    p.startsWith('/payments/') ||
    p === '/vouchers' ||
    p.startsWith('/vouchers/') ||
    p === '/invoices' ||
    p.startsWith('/invoices/')
  ) {
    keys.push(ADMIN_SIDEBAR_GROUP_KEYS.salesTransactions);
  }
  if (
    p === '/rksv/sb/startbeleg' ||
    p === '/rksv/sb/monatsbeleg' ||
    p === '/rksv/sb/jahresbeleg' ||
    p === '/rksv/sb/nullbeleg' ||
    p === '/rksv/sb/schlussbeleg' ||
    p === '/rksv/sb/test-helper' ||
    p === '/rksv/sonderbelege' ||
    p.startsWith('/rksv/sonderbelege/')
  ) {
    keys.push(ADMIN_SIDEBAR_GROUP_KEYS.specialReceipts);
  }
  if (p === '/admin/rksv' || p.startsWith('/admin/rksv/')) {
    keys.push('/admin/rksv');
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
  canShowPlatformAdminMenu: (role: string) => boolean;
  isSuperAdminRole: (role: string) => boolean;
};

/**
 * Recursively filter menu items by permission / role. Submenus with no visible children are removed.
 */
export function filterSidebarMenuItems(
  items: MenuProps['items'] | undefined,
  ctx: SidebarPermissionContext
): MenuProps['items'] {
  if (!items?.length) return items;

  const isStructuralSubmenuKey = (key: string) =>
    key.startsWith('grp-') || key.startsWith('rksv-grp-');

  const platformAdminKeys = new Set([
    '/admin/tenants',
    '/admin/errors',
    '/admin/licenses',
    '/admin/cash-registers',
  ]);

  const leafAllowed = (key: string): boolean => {
    if (ctx.isSuperAdminRole(ctx.userRole)) return true;
    if (platformAdminKeys.has(key)) return ctx.canShowPlatformAdminMenu(ctx.userRole);
    if (ctx.usePermissionFirst) {
      return ctx.isMenuItemAllowed(key, ctx.permissions);
    }
    if (!ALLOW_EMPTY_PERMISSIONS_FOR_ROUTE_ACCESS) {
      if (isStructuralSubmenuKey(key)) {
        return true;
      }
      return false;
    }
    if (key === '/users' || key === '/admin/users') return ctx.canViewUsers(ctx.userRole);
    if (key === '/rksv' || key.startsWith('/rksv/')) {
      return isRksvRouteKeyAllowed(key, ctx.permissions, ctx.userRole);
    }
    if (
      key === '/admin/tenants' ||
      key === '/admin/errors' ||
      key === '/admin/licenses' ||
      key === '/admin/cash-registers'
    ) {
      return ctx.canShowPlatformAdminMenu(ctx.userRole);
    }
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
    for (const k of Array.from(keys)) {
      if (k === '/rksv' || k.startsWith('rksv-grp-')) {
        keys.delete(k);
      }
    }
  }

  /** Keep RKSV section open on audit/TSE routes or `/rksv/*` when allowed. */
  const rksvActive =
    p === '/audit-logs' ||
    p.startsWith('/audit-logs/') ||
    p === '/admin/audit/fiscal-exports' ||
    p.startsWith('/admin/audit/') ||
    p.startsWith('/admin/tse/') ||
    p === '/admin/tse/offline-transactions' ||
    p === '/admin/rksv' ||
    p.startsWith('/admin/rksv/') ||
    p === '/reporting/tagesbericht' ||
    p.startsWith('/reporting/tagesbericht/') ||
    p === '/reporting/monatsbericht' ||
    p.startsWith('/reporting/monatsbericht/') ||
    p === '/reporting/jahresbericht' ||
    p.startsWith('/reporting/jahresbericht/') ||
    (params.canSeeRksv && p.startsWith('/rksv'));
  if (!rksvActive) {
    keys.delete(ADMIN_SIDEBAR_GROUP_KEYS.rksv);
  }

  for (const k of getNonRksvSidebarOpenGroupKeys(p)) {
    keys.add(k);
  }

  if (!params.canSeeRksv && p.startsWith('/rksv')) {
    keys.delete(ADMIN_SIDEBAR_GROUP_KEYS.rksv);
  }

  const sonderOnly = p === '/rksv/sonderbelege' || p.startsWith('/rksv/sonderbelege/');

  if (params.canSeeRksv && p.startsWith('/rksv') && !sonderOnly) {
    keys.add(ADMIN_SIDEBAR_GROUP_KEYS.rksv);
    keys.add('/rksv');
    for (const k of getRksvOpenSubgroupKeys(p, params.rksvGroups)) {
      keys.add(k);
    }
  }

  /** Sonderbelege deep links: open special-receipts subgroup without expanding the full RKSV hub. */
  if (params.canSeeRksv && sonderOnly) {
    keys.add(ADMIN_SIDEBAR_GROUP_KEYS.rksv);
    keys.add(ADMIN_SIDEBAR_GROUP_KEYS.specialReceipts);
  }

  return Array.from(keys);
}
