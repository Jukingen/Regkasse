/**
 * Centralized logical menu-area → permission mapping for FA.
 *
 * Path-level gates remain in `routePermissions.ROUTE_PERMISSIONS` (and the deprecated
 * `MENU_PERMISSION` proxy). This registry is the IA-level source of truth for sidebar
 * groups / feature areas (Betrieb, RKSV, Sortiment, …).
 *
 * Permission strings must match `AppPermissions.cs` / `permissions.ts` — do not invent
 * keys such as `dashboard.view`, `users.view`, `cashregister.view`, `rksv.view`,
 * `backup.view`, or `billing.view`.
 *
 * Docs: `docs/PERMISSIONS_MENU_MAPPING.md`
 */
import {
  AppPermissions,
  type UserWithPermissions,
  hasAnyPermission,
  hasPermission,
  PERMISSIONS,
} from './permissions';

/** Single area gate: one permission, OR list, and/or authenticated fallback. */
export type MenuPermissionEntry = {
  /** Primary permission(s). Array = any-of (OR). */
  permission?: string | readonly string[];
  /**
   * When true, allow any authenticated user with at least one permission claim
   * even if the listed permission is missing (e.g. Operations / dashboard hub).
   */
  fallback?: boolean;
};

/**
 * Logical menu areas → required permission(s).
 * Keys are stable IA ids (not App Router paths).
 */
export const MENU_PERMISSIONS = {
  // Betrieb
  /** Dashboard — any authenticated admin with permission claims. */
  dashboard: { fallback: true } satisfies MenuPermissionEntry,
  /**
   * Operations Center hub — matches `/operations-center` OR gate.
   * (Not authenticated-fallback; that is `dashboard`.)
   */
  operations: {
    permission: [
      PERMISSIONS.SALE_VIEW,
      PERMISSIONS.TSE_SIGN,
      PERMISSIONS.RECEIPT_REPRINT,
      PERMISSIONS.REPORT_EXPORT,
    ],
  },
  tables: { permission: PERMISSIONS.TABLE_VIEW },
  /** Aligns with `/kassenverwaltung` route gate (`cash_register.manage`). */
  cashRegisters: { permission: AppPermissions.CashRegisterManage },
  /** Staff hub — any of list / performance / shifts gates. */
  employees: {
    permission: [PERMISSIONS.USER_VIEW, PERMISSIONS.REPORT_VIEW, PERMISSIONS.SHIFT_VIEW],
  },
  shifts: { permission: PERMISSIONS.SHIFT_VIEW },
  sales: { permission: PERMISSIONS.SALE_VIEW },
  /** Tagesabschluss leaf — `/tagesabschluss` → `daily-closing.view`. */
  tagesabschluss: { permission: PERMISSIONS.DAILY_CLOSING_VIEW },

  // RKSV & FinanzOnline — no `rksv.view`; hub matches `/rksv` → finanzonline.manage
  rksv: { permission: PERMISSIONS.FINANZONLINE_MANAGE },
  finanzOnline: { permission: PERMISSIONS.FINANZONLINE_VIEW },

  // Sortiment & Preise
  products: { permission: PERMISSIONS.PRODUCT_VIEW },
  categories: { permission: PERMISSIONS.CATEGORY_VIEW },

  // Kunden & Vorteile
  customers: { permission: PERMISSIONS.CUSTOMER_VIEW },
  benefits: { permission: PERMISSIONS.BENEFIT_VIEW },

  // Berichte
  reports: { permission: PERMISSIONS.REPORT_VIEW },

  // Backup — no `backup.view`; hub reads use settings.view (manage = backup.manage)
  backup: { permission: PERMISSIONS.SETTINGS_VIEW },

  // Einstellungen
  settings: { permission: PERMISSIONS.SETTINGS_VIEW },
  workingHours: { permission: PERMISSIONS.SETTINGS_VIEW },
  digitalServices: {
    permission: [
      PERMISSIONS.DIGITAL_VIEW,
      PERMISSIONS.DIGITAL_REQUEST,
      PERMISSIONS.WEBSITE_MANAGE,
    ],
  },

  // Verwaltung — `/admin/tenants` is Super Admin (`system.critical`); backend also has `tenant.view`
  tenants: { permission: PERMISSIONS.SYSTEM_CRITICAL },
  users: { permission: PERMISSIONS.USER_VIEW },
  license: { permission: PERMISSIONS.LICENSE_VIEW },
  /** No `billing.view` in catalog — platform billing is Super Admin (`system.critical`). */
  billing: { permission: PERMISSIONS.SYSTEM_CRITICAL },
} as const satisfies Record<string, MenuPermissionEntry>;

export type MenuAreaKey = keyof typeof MENU_PERMISSIONS;

export type MenuPermissionState = {
  visible: boolean;
  /** Primary (first) required permission string; empty when fallback-only / unknown. */
  permission: string;
};

export function getMenuPermissionEntry(area: MenuAreaKey): MenuPermissionEntry {
  return MENU_PERMISSIONS[area];
}

/** Normalize entry permission to a string array (empty = none required beyond fallback rules). */
export function resolveMenuAreaPermissions(area: MenuAreaKey): string[] {
  const entry = MENU_PERMISSIONS[area];
  const raw = entry.permission;
  if (raw === undefined) return [];
  return Array.isArray(raw) ? [...raw] : [raw];
}

/**
 * Primary App Router paths for each logical area (representative leaf / hub).
 * Path-level authority remains `ROUTE_PERMISSIONS`; this is for docs and coarse checks.
 */
export const MENU_AREA_PRIMARY_PATH: Record<MenuAreaKey, string> = {
  dashboard: '/dashboard',
  operations: '/operations-center',
  tables: '/tables',
  cashRegisters: '/kassenverwaltung',
  employees: '/staff',
  shifts: '/shifts',
  sales: '/receipts',
  tagesabschluss: '/tagesabschluss',
  rksv: '/rksv',
  finanzOnline: '/rksv/finanz-online-operations',
  products: '/products',
  categories: '/categories',
  customers: '/customers',
  benefits: '/benefit-definitions',
  reports: '/reporting',
  backup: '/backup',
  settings: '/settings',
  workingHours: '/settings/working-hours',
  digitalServices: '/settings/digital',
  tenants: '/admin/tenants',
  users: '/admin/users',
  license: '/admin/licenses',
  billing: '/admin/billing',
};

const PATH_TO_MENU_AREA: ReadonlyMap<string, MenuAreaKey> = new Map(
  (Object.entries(MENU_AREA_PRIMARY_PATH) as [MenuAreaKey, string][]).map(([area, path]) => [
    path,
    area,
  ])
);

function isMenuAreaKey(key: string): key is MenuAreaKey {
  return Object.prototype.hasOwnProperty.call(MENU_PERMISSIONS, key);
}

/**
 * Resolve a registry area from a logical key (`tagesabschluss`) or exact primary path
 * (`/tagesabschluss`). Nested paths are intentionally not mapped — use `ROUTE_PERMISSIONS`
 * for leaf-specific gates (e.g. `/tagesabschluss/execute`).
 */
export function resolveMenuAreaKey(menuKey: string): MenuAreaKey | undefined {
  if (!menuKey) return undefined;

  if (isMenuAreaKey(menuKey)) return menuKey;

  const path = menuKey.startsWith('/') ? menuKey : `/${menuKey}`;
  return PATH_TO_MENU_AREA.get(path);
}

/**
 * Claim-based visibility for a registry area (implication-aware via `hasPermission`).
 * Super Admin bypass is left to callers (`useMenuPermissions` / sidebar).
 */
export function canAccessMenuArea(
  area: MenuAreaKey,
  user: UserWithPermissions | null | undefined
): boolean {
  return getMenuPermissionState(area, user).visible;
}

/**
 * Shared resolver used by `useMenuPermissions` and sidebar filtering.
 * Uses implication-aware `hasPermission` / `hasAnyPermission`.
 */
export function getMenuPermissionState(
  menuKey: MenuAreaKey | string,
  user: UserWithPermissions | null | undefined,
  options?: { isSuperAdmin?: boolean }
): MenuPermissionState {
  const area = resolveMenuAreaKey(String(menuKey));
  if (!area) {
    return { visible: false, permission: '' };
  }

  const required = resolveMenuAreaPermissions(area);
  const permission = required[0] ?? '';
  const entry = MENU_PERMISSIONS[area];
  const claims = user?.permissions;

  if (options?.isSuperAdmin) {
    return { visible: true, permission };
  }

  if (required.length > 0) {
    const allowed =
      required.length === 1
        ? hasPermission(user, required[0]!)
        : hasAnyPermission(user, required);
    if (allowed) return { visible: true, permission };
  }

  if (entry.fallback) {
    return { visible: Boolean(claims && claims.length > 0), permission };
  }

  if (required.length === 0) {
    return { visible: Boolean(claims && claims.length > 0), permission };
  }

  return { visible: false, permission };
}

/**
 * Sidebar leaf helper: registry when the key maps to an area, else `undefined`
 * so callers can fall back to path-level `isMenuItemAllowed` / `ROUTE_PERMISSIONS`.
 */
export function tryRegistryMenuVisibility(
  menuKey: string,
  user: UserWithPermissions | null | undefined,
  options?: { isSuperAdmin?: boolean }
): boolean | undefined {
  const area = resolveMenuAreaKey(menuKey);
  if (!area) return undefined;
  return getMenuPermissionState(area, user, options).visible;
}

export {
  logMenuPermissionMappingWarnings,
  validateMenuPermissionMappings,
  type MenuPermissionMappingIssue,
} from './menuPermissionMappingValidation';
