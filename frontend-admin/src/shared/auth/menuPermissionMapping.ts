/**
 * FA sidebar menu-area → permission mapping table (documentation + coarse UI).
 *
 * Runtime visibility: `menuPermissionRegistry.MENU_PERMISSIONS` /
 * `getMenuPermissionState`. Group sync: `permissionGroupRegistry`.
 * Path gates: `routePermissions.ROUTE_PERMISSIONS`.
 *
 * Keys are `MenuAreaKey` values. Permission strings must match
 * `AppPermissions.cs` / `permissions.ts` — do **not** invent
 * `dashboard.view`, `users.view`, `cashregister.view`, `rksv.view`,
 * `backup.view`, `dailyclosing.*`, or `sale.manage`.
 *
 * Docs: `docs/PERMISSIONS_MENU_MAPPING.md`
 */
import type { MenuAreaKey } from './menuPermissionRegistry';
import type { PermissionGroupKey } from './permissionGroupRegistry';
import { AppPermissions, PERMISSIONS } from './permissions';

export type MenuPermissionMapEntry = {
  /** German sidebar label (default locale). */
  menuLabel: string;
  /**
   * Catalog group slug, or `null` for authenticated / multi-permission hubs
   * with no dedicated group.
   */
  permissionGroup: PermissionGroupKey | null;
  /**
   * Primary gate for this menu area (first of OR-list when multiple).
   * Empty string = authenticated fallback / hub without a single key.
   */
  permissionKey: string;
  /** Additional any-of gates (OR) when the hub accepts several permissions. */
  permissionKeysAnyOf?: readonly string[];
  /**
   * Holder permission(s) that imply `permissionKey` via `permissionImplication`
   * (e.g. `user.manage` → `user.view`). Informational; runtime uses implication map.
   */
  impliedBy?: string | readonly string[];
  /**
   * When false, area may show for any authenticated admin with claims
   * (`fallback` / hub) even without the listed key.
   */
  required?: boolean;
  /** Super Admin / `system.critical` surfaces only. */
  superAdminOnly?: boolean;
};

/**
 * Logical sidebar areas → catalog permission mapping.
 * Keep aligned with `MENU_PERMISSIONS` + `MENU_AREA_TO_PERMISSION_GROUP`.
 */
export const MENU_PERMISSION_MAP = {
  // === Betrieb ===
  operations: {
    menuLabel: 'Operations Center',
    permissionGroup: null,
    permissionKey: PERMISSIONS.SALE_VIEW,
    permissionKeysAnyOf: [
      PERMISSIONS.SALE_VIEW,
      PERMISSIONS.TSE_SIGN,
      PERMISSIONS.RECEIPT_REPRINT,
      PERMISSIONS.REPORT_EXPORT,
    ],
    required: true,
  },
  tables: {
    menuLabel: 'Tische',
    permissionGroup: 'bestellung_verkauf',
    permissionKey: PERMISSIONS.TABLE_VIEW,
    impliedBy: PERMISSIONS.TABLE_MANAGE,
    required: true,
  },
  cashRegisters: {
    menuLabel: 'Kassenverwaltung',
    permissionGroup: 'kassenverwaltung',
    /** Route/menu gate is manage (not view-only). `cash_register.manage` implies `.view`. */
    permissionKey: AppPermissions.CashRegisterManage,
    required: true,
  },
  employees: {
    menuLabel: 'Mitarbeiter',
    permissionGroup: 'mitarbeiter',
    permissionKey: PERMISSIONS.USER_VIEW,
    permissionKeysAnyOf: [
      PERMISSIONS.USER_VIEW,
      PERMISSIONS.REPORT_VIEW,
      PERMISSIONS.SHIFT_VIEW,
    ],
    impliedBy: PERMISSIONS.USER_MANAGE,
    required: true,
  },
  shifts: {
    menuLabel: 'Schichten & Abschlüsse',
    permissionGroup: 'kassenverwaltung',
    permissionKey: PERMISSIONS.SHIFT_VIEW,
    impliedBy: PERMISSIONS.SHIFT_MANAGE,
    required: true,
  },
  sales: {
    menuLabel: 'Verkauf & Vorgänge',
    permissionGroup: 'bestellung_verkauf',
    permissionKey: PERMISSIONS.SALE_VIEW,
    // No `sale.manage` in catalog — writes use `sale.create` / leaf gates.
    required: true,
  },
  tagesabschluss: {
    menuLabel: 'Tagesabschluss',
    permissionGroup: 'tagesabschluss',
    permissionKey: PERMISSIONS.DAILY_CLOSING_VIEW,
    impliedBy: PERMISSIONS.DAILY_CLOSING_EXECUTE,
    required: true,
  },
  rksv: {
    menuLabel: 'RKSV & FinanzOnline',
    permissionGroup: 'rksv_finanzonline',
    /** No `rksv.view` — hub matches finanzonline.manage. */
    permissionKey: PERMISSIONS.FINANZONLINE_MANAGE,
    required: true,
  },

  // === Sortiment & Preise ===
  products: {
    menuLabel: 'Produkte',
    permissionGroup: 'sortiment_preise',
    permissionKey: PERMISSIONS.PRODUCT_VIEW,
    impliedBy: PERMISSIONS.PRODUCT_MANAGE,
    required: true,
  },
  categories: {
    menuLabel: 'Kategorien',
    permissionGroup: 'sortiment_preise',
    permissionKey: PERMISSIONS.CATEGORY_VIEW,
    impliedBy: PERMISSIONS.CATEGORY_MANAGE,
    required: true,
  },

  // === Kunden & Vorteile ===
  customers: {
    menuLabel: 'Kunden',
    permissionGroup: 'kunden_vorteile',
    permissionKey: PERMISSIONS.CUSTOMER_VIEW,
    impliedBy: PERMISSIONS.CUSTOMER_MANAGE,
    required: true,
  },
  benefits: {
    menuLabel: 'Vorteile',
    permissionGroup: 'kunden_vorteile',
    permissionKey: PERMISSIONS.BENEFIT_VIEW,
    impliedBy: PERMISSIONS.BENEFIT_MANAGE,
    required: true,
  },

  // === Berichte ===
  reports: {
    menuLabel: 'Berichte & Auswertungen',
    permissionGroup: 'audit_berichte',
    permissionKey: PERMISSIONS.REPORT_VIEW,
    impliedBy: PERMISSIONS.REPORT_EXPORT,
    required: true,
  },

  // === Backup ===
  backup: {
    menuLabel: 'Backup & Disaster Recovery',
    permissionGroup: 'backup_disaster_recovery',
    /** No `backup.view` — hub reads use settings.view; manage = backup.manage. */
    permissionKey: PERMISSIONS.SETTINGS_VIEW,
    impliedBy: [PERMISSIONS.BACKUP_MANAGE, PERMISSIONS.SETTINGS_MANAGE],
    required: true,
  },

  // === Einstellungen ===
  settings: {
    menuLabel: 'Einstellungen',
    permissionGroup: 'einstellungen',
    permissionKey: PERMISSIONS.SETTINGS_VIEW,
    impliedBy: PERMISSIONS.SETTINGS_MANAGE,
    required: true,
  },
  workingHours: {
    menuLabel: 'Öffnungszeiten',
    permissionGroup: 'einstellungen',
    permissionKey: PERMISSIONS.SETTINGS_VIEW,
    impliedBy: PERMISSIONS.SETTINGS_MANAGE,
    required: true,
  },
  digitalServices: {
    menuLabel: 'Digitale Dienste',
    permissionGroup: 'digitale_dienste',
    permissionKey: PERMISSIONS.DIGITAL_VIEW,
    permissionKeysAnyOf: [
      PERMISSIONS.DIGITAL_VIEW,
      PERMISSIONS.DIGITAL_REQUEST,
      PERMISSIONS.WEBSITE_MANAGE,
    ],
    impliedBy: PERMISSIONS.DIGITAL_MANAGE,
    required: true,
  },

  // === Verwaltung ===
  tenants: {
    menuLabel: 'Mandanten',
    permissionGroup: 'system',
    /** FA leaf is Super Admin (`system.critical`); catalog also has `tenant.view`. */
    permissionKey: PERMISSIONS.SYSTEM_CRITICAL,
    required: true,
    superAdminOnly: true,
  },
  users: {
    menuLabel: 'Benutzer',
    permissionGroup: 'mitarbeiter',
    permissionKey: PERMISSIONS.USER_VIEW,
    impliedBy: PERMISSIONS.USER_MANAGE,
    required: true,
    superAdminOnly: true,
  },
  license: {
    menuLabel: 'Lizenzverwaltung',
    permissionGroup: 'einstellungen',
    permissionKey: PERMISSIONS.LICENSE_VIEW,
    impliedBy: PERMISSIONS.LICENSE_MANAGE,
    required: true,
    superAdminOnly: true,
  },
} as const satisfies Partial<Record<MenuAreaKey, MenuPermissionMapEntry>>;

export type MenuPermissionMapKey = keyof typeof MENU_PERMISSION_MAP;

export function isMenuPermissionMapKey(key: string): key is MenuPermissionMapKey {
  return Object.prototype.hasOwnProperty.call(MENU_PERMISSION_MAP, key);
}

export function getMenuPermissionMapEntry(
  area: MenuPermissionMapKey
): MenuPermissionMapEntry {
  return MENU_PERMISSION_MAP[area];
}

/** Primary permission key for a menu area, or `null` if unmapped / empty. */
export function getPermissionForMenu(menuKey: string): string | null {
  if (!isMenuPermissionMapKey(menuKey)) return null;
  return MENU_PERMISSION_MAP[menuKey].permissionKey || null;
}

/** Menu area keys whose primary `permissionKey` equals the given catalog key. */
export function getMenusForPermission(permissionKey: string): string[] {
  return (Object.entries(MENU_PERMISSION_MAP) as [MenuPermissionMapKey, MenuPermissionMapEntry][])
    .filter(([, mapping]) => mapping.permissionKey === permissionKey)
    .map(([key]) => key);
}

/** Flat rows for docs / debug tables. */
export function listMenuPermissionMapRows(): Array<
  MenuPermissionMapEntry & { menuKey: MenuPermissionMapKey }
> {
  return (Object.keys(MENU_PERMISSION_MAP) as MenuPermissionMapKey[]).map((menuKey) => ({
    menuKey,
    ...MENU_PERMISSION_MAP[menuKey],
  }));
}

/** All IA keys declared in {@link MENU_PERMISSION_MAP}. */
export function getAllMenuKeys(): MenuPermissionMapKey[] {
  return Object.keys(MENU_PERMISSION_MAP) as MenuPermissionMapKey[];
}

/**
 * Catalog `permission` value for a mapped area (OR-list when `permissionKeysAnyOf` is set).
 */
export function catalogPermissionFromMap(
  area: MenuPermissionMapKey
): string | string[] {
  const entry = MENU_PERMISSION_MAP[area];
  if (entry.permissionKeysAnyOf && entry.permissionKeysAnyOf.length > 0) {
    return [...entry.permissionKeysAnyOf];
  }
  return entry.permissionKey;
}

/**
 * Sidebar fields derived from {@link MENU_PERMISSION_MAP} (single source of truth).
 */
export function sidebarFieldsFromMenuMap(area: MenuPermissionMapKey): {
  menuArea: MenuPermissionMapKey;
  permission: string | string[];
  permissionGroup: PermissionGroupKey | null;
} {
  const entry = MENU_PERMISSION_MAP[area];
  return {
    menuArea: area,
    permission: catalogPermissionFromMap(area),
    permissionGroup: entry.permissionGroup,
  };
}

/**
 * Returns sidebar menu-area keys that are missing from {@link MENU_PERMISSION_MAP}.
 * When `menuKeys` is omitted, validates {@link getAllMenuKeys} (always empty).
 */
export function validateMenuPermissions(menuKeys?: readonly string[]): string[] {
  const keys = menuKeys ?? getAllMenuKeys();
  const missingMappings = keys.filter((key) => !isMenuPermissionMapKey(key));
  if (missingMappings.length > 0) {
    // eslint-disable-next-line no-console -- intentional mapping audit
    console.warn('Missing menu-permission mappings:', missingMappings);
  }
  return missingMappings;
}

/**
 * Map keys that are not wired onto any catalog leaf via `menuArea`.
 * Development-only callers should log these as warnings.
 */
export function listUnwiredMenuPermissionMapKeys(
  wiredMenuAreas: readonly string[]
): MenuPermissionMapKey[] {
  const wired = new Set(wiredMenuAreas);
  return getAllMenuKeys().filter((key) => !wired.has(key));
}

/** Dev-only console warnings for missing / unwired menu↔permission mappings. */
export function logMenuPermissionMapWarnings(wiredMenuAreas: readonly string[]): void {
  validateMenuPermissions(wiredMenuAreas);
  const unwired = listUnwiredMenuPermissionMapKeys(wiredMenuAreas);
  if (unwired.length > 0) {
    // eslint-disable-next-line no-console -- intentional mapping audit
    console.warn(
      'MENU_PERMISSION_MAP keys not wired into SIDEBAR_NAV_ITEM_CATALOG.menuArea:',
      unwired
    );
  }
}
