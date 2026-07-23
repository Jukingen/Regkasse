/**
 * Maps a permission key to FA menu items it can unlock (sidebar catalog + route gates).
 */
import {
  SIDEBAR_NAV_ITEM_CATALOG,
  type SidebarIconToken,
} from '@/shared/adminSidebarRegistry';
import {
  MENU_AREA_PRIMARY_PATH,
  MENU_PERMISSIONS,
  type MenuAreaKey,
  resolveMenuAreaPermissions,
} from '@/shared/auth/menuPermissionRegistry';
import {
  PERMISSION_GROUPS,
  type PermissionGroupKey,
} from '@/shared/auth/permissionGroupRegistry';
import {
  MENU_PERMISSION_MAP,
  isMenuPermissionMapKey,
} from '@/shared/auth/menuPermissionMapping';
import { permissionImplied } from '@/shared/auth/permissionImplication';
import { ROUTE_PERMISSIONS } from '@/shared/auth/routePermissions';

export type PermissionMenuImpactItem = {
  /** App Router / menu key path */
  path: string;
  /** i18n key, e.g. nav.customers */
  labelKey: string;
  /** Sidebar icon token when known from catalog / group. */
  icon?: SidebarIconToken;
};

/** Menu chip for a permission-group header (IA areas linked to the group). */
export type PermissionGroupMenuChip = {
  key: string;
  labelKey: string;
  icon?: SidebarIconToken;
};

/** Fallback labels when a menu area is not already covered by the sidebar catalog. */
export const MENU_AREA_LABEL_KEYS: Record<MenuAreaKey, string> = {
  dashboard: 'nav.dashboard',
  operations: 'nav.operationsCenter',
  tables: 'nav.tables',
  cashRegisters: 'nav.cashRegisters',
  employees: 'nav.staff',
  shifts: 'nav.shifts',
  sales: 'nav.receipts',
  tagesabschluss: 'nav.dailyClosing',
  rksv: 'nav.rksv',
  finanzOnline: 'nav.finanzOnline',
  products: 'nav.products',
  categories: 'nav.categories',
  customers: 'nav.customerList',
  benefits: 'nav.benefitDefinitions',
  reports: 'nav.operationalReports',
  backup: 'nav.backupDisasterRecovery',
  settings: 'nav.settings',
  workingHours: 'nav.workingHours',
  digitalServices: 'nav.digital',
  tenants: 'nav.tenants',
  users: 'nav.users',
  license: 'nav.licenses',
  billing: 'nav.billingHub',
};

function requirementList(required: string | readonly string[] | undefined): string[] {
  if (required === undefined) return [];
  if (Array.isArray(required)) {
    // Empty array = any-authenticated (no specific permission gate).
    if (required.length === 0) return [];
    return required.filter((r): r is string => typeof r === 'string' && r.length > 0);
  }
  return required.length > 0 ? [required] : [];
}

/** True when holding `permission` alone satisfies a route/menu requirement. */
export function permissionUnlocksRequirement(
  permission: string,
  required: string | readonly string[] | undefined
): boolean {
  const list = requirementList(required);
  if (list.length === 0) return false;
  return list.some((r) => permissionImplied(r, [permission]));
}

function catalogIconForPath(path: string): SidebarIconToken | undefined {
  for (const item of Object.values(SIDEBAR_NAV_ITEM_CATALOG)) {
    if (item.menuKey === path && item.icon) return item.icon;
  }
  return undefined;
}

/**
 * Returns distinct menu items affected by the given permission (label keys for i18n).
 * Prefer sidebar catalog labels; fall back to area primary paths.
 */
export function getMenuItemsAffectedByPermission(
  permission: string
): PermissionMenuImpactItem[] {
  if (!permission.trim()) return [];

  const byPath = new Map<string, PermissionMenuImpactItem>();

  for (const item of Object.values(SIDEBAR_NAV_ITEM_CATALOG)) {
    if (item.sidebarHidden) continue;
    const required = item.permission ?? ROUTE_PERMISSIONS[item.menuKey];
    if (!permissionUnlocksRequirement(permission, required)) continue;
    byPath.set(item.menuKey, {
      path: item.menuKey,
      labelKey: item.labelKey,
      icon: item.icon,
    });
  }

  for (const area of Object.keys(MENU_PERMISSIONS) as MenuAreaKey[]) {
    const required = resolveMenuAreaPermissions(area);
    if (!permissionUnlocksRequirement(permission, required)) continue;
    const path = MENU_AREA_PRIMARY_PATH[area];
    if (byPath.has(path)) continue;
    byPath.set(path, {
      path,
      labelKey: MENU_AREA_LABEL_KEYS[area],
      icon: catalogIconForPath(path),
    });
  }

  return [...byPath.values()].sort((a, b) => a.path.localeCompare(b.path));
}

/**
 * Related sidebar menu chips for a permission catalog group slug
 * (`PERMISSION_GROUPS.menuKeys`).
 */
export function getMenuChipsForPermissionGroup(
  groupSlug: string
): PermissionGroupMenuChip[] {
  const def = PERMISSION_GROUPS[groupSlug as PermissionGroupKey];
  if (!def?.menuKeys.length) return [];

  return def.menuKeys.map((area) => {
    const path = MENU_AREA_PRIMARY_PATH[area];
    const catalogItem = Object.values(SIDEBAR_NAV_ITEM_CATALOG).find(
      (item) => item.menuArea === area || item.menuKey === path
    );
    return {
      key: area,
      labelKey: catalogItem?.labelKey ?? MENU_AREA_LABEL_KEYS[area],
      icon: catalogItem?.icon ?? def.icon,
    };
  });
}

export type MenuPermissionRequirement = {
  /** Catalog permission key (e.g. daily-closing.view). */
  key: string;
  /** True when this is the primary/view gate (first in OR list). */
  primary: boolean;
};

export type SidebarMenuFilterOption = {
  /** Sidebar / route menuKey path. */
  value: string;
  labelKey: string;
  icon?: SidebarIconToken;
  permissionGroup: PermissionGroupKey | null;
  /** Primary permission key when known. */
  primaryPermission: string | null;
  /** Whether the catalog leaf has no explicit permission mapping. */
  missingPermission: boolean;
};

function findCatalogByMenuKey(menuKey: string) {
  return Object.values(SIDEBAR_NAV_ITEM_CATALOG).find((item) => item.menuKey === menuKey);
}

/**
 * Permissions that gate a sidebar leaf (catalog `permission` or `ROUTE_PERMISSIONS`).
 * Includes related manage/execute keys from `MENU_PERMISSION_MAP.impliedBy` when present.
 */
export function getPermissionsAffectingMenu(menuKey: string): MenuPermissionRequirement[] {
  if (!menuKey.trim()) return [];

  const catalog = findCatalogByMenuKey(menuKey);
  const required = catalog?.permission ?? ROUTE_PERMISSIONS[menuKey];
  const list = requirementList(required);

  const byKey = new Map<string, MenuPermissionRequirement>();
  list.forEach((key, index) => {
    byKey.set(key, { key, primary: index === 0 });
  });

  const area = catalog?.menuArea;
  if (area) {
    const areaRequired = resolveMenuAreaPermissions(area);
    for (const key of areaRequired) {
      if (!byKey.has(key)) byKey.set(key, { key, primary: byKey.size === 0 });
    }
    if (isMenuPermissionMapKey(area)) {
      const mapEntry = MENU_PERMISSION_MAP[area];
      const implied = mapEntry.impliedBy
        ? Array.isArray(mapEntry.impliedBy)
          ? [...mapEntry.impliedBy]
          : [mapEntry.impliedBy]
        : [];
      for (const key of implied) {
        if (!byKey.has(key)) byKey.set(key, { key, primary: false });
      }
      if (mapEntry.permissionKeysAnyOf) {
        for (const key of mapEntry.permissionKeysAnyOf) {
          if (!byKey.has(key)) byKey.set(key, { key, primary: byKey.size === 0 });
        }
      }
    }
  }

  return [...byKey.values()].sort((a, b) => {
    if (a.primary !== b.primary) return a.primary ? -1 : 1;
    return a.key.localeCompare(b.key);
  });
}

/** True when holding any of `held` unlocks the menu leaf. */
export function userCanSeeMenuWithPermissions(
  menuKey: string,
  held: readonly string[] | undefined
): boolean {
  const required = getPermissionsAffectingMenu(menuKey).map((r) => r.key);
  if (required.length === 0) return Boolean(held && held.length > 0);
  if (!held?.length) return false;
  return required.some((r) => permissionImplied(r, held));
}

/**
 * Visible sidebar leaves as filter options (for permission UI Menü Select + explorer).
 */
export function listSidebarMenuFilterOptions(): SidebarMenuFilterOption[] {
  const options: SidebarMenuFilterOption[] = [];

  for (const item of Object.values(SIDEBAR_NAV_ITEM_CATALOG)) {
    if (item.sidebarHidden) continue;
    if (item.developmentOnly && process.env.NODE_ENV !== 'development') continue;

    const requirements = getPermissionsAffectingMenu(item.menuKey);

    let permissionGroup: PermissionGroupKey | null = item.permissionGroup ?? null;
    if (!permissionGroup && item.menuArea) {
      for (const [slug, def] of Object.entries(PERMISSION_GROUPS) as [
        PermissionGroupKey,
        (typeof PERMISSION_GROUPS)[PermissionGroupKey],
      ][]) {
        if (def.menuKeys.includes(item.menuArea)) {
          permissionGroup = slug;
          break;
        }
      }
    }

    options.push({
      value: item.menuKey,
      labelKey: item.labelKey,
      icon: item.icon,
      permissionGroup,
      primaryPermission: requirements[0]?.key ?? null,
      missingPermission: requirements.length === 0,
    });
  }

  return options.sort((a, b) => a.value.localeCompare(b.value));
}

/** Roles whose permission set satisfies `permissionKey` (implication-aware). */
export function listRolesHoldingPermission(
  permissionKey: string,
  roles: ReadonlyArray<{
    roleName?: string | null;
    displayName?: string | null;
    permissions?: string[] | null;
  }>
): Array<{ roleName: string; displayName: string }> {
  if (!permissionKey.trim()) return [];
  const out: Array<{ roleName: string; displayName: string }> = [];
  for (const role of roles) {
    const name = role.roleName?.trim();
    if (!name) continue;
    const held = role.permissions ?? [];
    if (!permissionImplied(permissionKey, held)) continue;
    out.push({
      roleName: name,
      displayName: role.displayName?.trim() || name,
    });
  }
  return out.sort((a, b) => a.displayName.localeCompare(b.displayName));
}
