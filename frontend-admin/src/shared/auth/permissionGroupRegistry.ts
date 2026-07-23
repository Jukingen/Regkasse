/**
 * Permission catalog groups ↔ FA sidebar IA sync.
 *
 * Mirrors backend `PermissionCatalogMetadata.ResourceToGroup` (display names + slugs).
 * Links each group to `menuPermissionRegistry` menu areas and optional sidebar group ids.
 *
 * Do **not** invent keys (`users.view`, `cashregister.view`, `roles.view`, …).
 * Canonical: `user.view`, `cash_register.manage`, `role.view`, …
 *
 * Docs: `docs/PERMISSIONS_MENU_MAPPING.md`
 */
import type { SidebarGroupId, SidebarIconToken } from '@/shared/adminSidebarRegistry';
import type { MenuAreaKey } from '@/shared/auth/menuPermissionRegistry';
import { AppPermissions, PERMISSIONS } from '@/shared/auth/permissions';

export type PermissionGroupKey =
  | 'mitarbeiter'
  | 'kassenverwaltung'
  | 'bestellung_verkauf'
  | 'zahlung'
  | 'tagesabschluss'
  | 'rksv_finanzonline'
  | 'sortiment_preise'
  | 'lager'
  | 'kunden_vorteile'
  | 'audit_berichte'
  | 'backup_disaster_recovery'
  | 'einstellungen'
  | 'digitale_dienste'
  | 'system'
  | 'sonstige'
  | 'other';

export type PermissionGroupDefinition = {
  /** Stable slug — matches `PermissionCatalogMetadata.GetGroupKey` / i18n `users.roleDrawer.groups.*`. */
  key: PermissionGroupKey;
  /** Backend / API catalog display name (German). */
  catalogDisplayName: string;
  /** Admin i18n key for localized group title. */
  labelKey: string;
  /** Sidebar icon token (`SIDEBAR_ICON_COMPONENTS`). */
  icon: SidebarIconToken;
  /**
   * Logical menu areas from `menuPermissionRegistry` primarily associated with this group.
   * Empty = no dedicated top-level menu (nested / catch-all groups).
   */
  menuKeys: readonly MenuAreaKey[];
  /** Optional FA sidebar shell group id(s) that surface this permission group. */
  sidebarGroupIds: readonly SidebarGroupId[];
  /**
   * Permission resource prefixes (before first `.`) — mirrors backend ResourceToGroup.
   * Used to classify catalog keys and menu route gates into groups.
   */
  resourcePrefixes: readonly string[];
  /** Representative view/manage keys for docs / coarse UI (not exhaustive catalog). */
  permissions: readonly string[];
};

/**
 * Canonical permission groups in sidebar-aligned display order.
 * Keep in sync with backend `PermissionCatalogMetadata` and `docs/PERMISSIONS_MENU_MAPPING.md`.
 */
export const PERMISSION_GROUPS = {
  mitarbeiter: {
    key: 'mitarbeiter',
    catalogDisplayName: 'Mitarbeiter',
    labelKey: 'users.roleDrawer.groups.mitarbeiter',
    icon: 'TeamOutlined',
    menuKeys: ['employees', 'users'],
    sidebarGroupIds: ['operations', 'administration'],
    resourcePrefixes: ['user', 'role'],
    permissions: [
      PERMISSIONS.USER_VIEW,
      PERMISSIONS.USER_MANAGE,
      PERMISSIONS.ROLE_VIEW,
      PERMISSIONS.ROLE_MANAGE,
    ],
  },
  kassenverwaltung: {
    key: 'kassenverwaltung',
    catalogDisplayName: 'Kassenverwaltung',
    labelKey: 'users.roleDrawer.groups.kassenverwaltung',
    icon: 'ShopOutlined',
    menuKeys: ['cashRegisters', 'shifts'],
    sidebarGroupIds: ['operations'],
    resourcePrefixes: ['cash_register', 'cashregister', 'cashdrawer', 'shift'],
    permissions: [
      AppPermissions.CashRegisterView,
      AppPermissions.CashRegisterManage,
      PERMISSIONS.SHIFT_VIEW,
      PERMISSIONS.SHIFT_MANAGE,
    ],
  },
  bestellung_verkauf: {
    key: 'bestellung_verkauf',
    catalogDisplayName: 'Bestellung & Verkauf',
    labelKey: 'users.roleDrawer.groups.bestellung_verkauf',
    icon: 'ShoppingCartOutlined',
    menuKeys: ['tables', 'sales'],
    sidebarGroupIds: ['operations'],
    resourcePrefixes: ['order', 'table', 'cart', 'sale', 'invoice', 'creditnote', 'kitchen'],
    permissions: [
      PERMISSIONS.TABLE_VIEW,
      PERMISSIONS.SALE_VIEW,
      PERMISSIONS.ORDER_VIEW,
      PERMISSIONS.INVOICE_VIEW,
    ],
  },
  zahlung: {
    key: 'zahlung',
    catalogDisplayName: 'Zahlung',
    labelKey: 'users.roleDrawer.groups.zahlung',
    icon: 'CreditCardOutlined',
    menuKeys: [],
    sidebarGroupIds: ['operations'],
    resourcePrefixes: ['payment', 'refund', 'discount', 'voucher'],
    permissions: [
      PERMISSIONS.PAYMENT_VIEW,
      PERMISSIONS.VOUCHER_READ,
      PERMISSIONS.REFUND_CREATE,
    ],
  },
  tagesabschluss: {
    key: 'tagesabschluss',
    catalogDisplayName: 'Tagesabschluss',
    labelKey: 'users.roleDrawer.groups.tagesabschluss',
    icon: 'CalendarOutlined',
    menuKeys: ['tagesabschluss'],
    sidebarGroupIds: ['operations'],
    resourcePrefixes: ['daily-closing'],
    permissions: [PERMISSIONS.DAILY_CLOSING_VIEW, PERMISSIONS.DAILY_CLOSING_EXECUTE],
  },
  rksv_finanzonline: {
    key: 'rksv_finanzonline',
    catalogDisplayName: 'RKSV & FinanzOnline',
    labelKey: 'users.roleDrawer.groups.rksv_finanzonline',
    icon: 'SafetyCertificateOutlined',
    menuKeys: ['rksv', 'finanzOnline'],
    sidebarGroupIds: ['rksv'],
    resourcePrefixes: ['finanzonline', 'tse', 'rksv'],
    permissions: [
      PERMISSIONS.FINANZONLINE_VIEW,
      PERMISSIONS.FINANZONLINE_MANAGE,
      PERMISSIONS.TSE_SIGN,
    ],
  },
  sortiment_preise: {
    key: 'sortiment_preise',
    catalogDisplayName: 'Sortiment & Preise',
    labelKey: 'users.roleDrawer.groups.sortiment_preise',
    icon: 'AppstoreOutlined',
    menuKeys: ['products', 'categories'],
    sidebarGroupIds: ['catalog'],
    resourcePrefixes: ['product', 'category', 'modifier'],
    permissions: [
      PERMISSIONS.PRODUCT_VIEW,
      PERMISSIONS.CATEGORY_VIEW,
      PERMISSIONS.MODIFIER_VIEW,
    ],
  },
  lager: {
    key: 'lager',
    catalogDisplayName: 'Lager',
    labelKey: 'users.roleDrawer.groups.lager',
    icon: 'InboxOutlined',
    menuKeys: [],
    sidebarGroupIds: ['catalog'],
    resourcePrefixes: ['inventory'],
    permissions: [PERMISSIONS.INVENTORY_VIEW, PERMISSIONS.INVENTORY_MANAGE],
  },
  kunden_vorteile: {
    key: 'kunden_vorteile',
    catalogDisplayName: 'Kunden & Vorteile',
    labelKey: 'users.roleDrawer.groups.kunden_vorteile',
    icon: 'GiftOutlined',
    menuKeys: ['customers', 'benefits'],
    sidebarGroupIds: ['customers'],
    resourcePrefixes: ['customer', 'benefit'],
    permissions: [
      PERMISSIONS.CUSTOMER_VIEW,
      PERMISSIONS.CUSTOMER_MANAGE,
      PERMISSIONS.BENEFIT_VIEW,
      PERMISSIONS.BENEFIT_MANAGE,
    ],
  },
  audit_berichte: {
    key: 'audit_berichte',
    catalogDisplayName: 'Audit & Berichte',
    labelKey: 'users.roleDrawer.groups.audit_berichte',
    icon: 'BarChartOutlined',
    menuKeys: ['reports'],
    sidebarGroupIds: ['reports', 'rksv'],
    resourcePrefixes: ['audit', 'report'],
    permissions: [
      PERMISSIONS.REPORT_VIEW,
      PERMISSIONS.REPORT_EXPORT,
      PERMISSIONS.AUDIT_VIEW,
    ],
  },
  backup_disaster_recovery: {
    key: 'backup_disaster_recovery',
    catalogDisplayName: 'Backup & Disaster Recovery',
    labelKey: 'users.roleDrawer.groups.backup_disaster_recovery',
    icon: 'CloudServerOutlined',
    menuKeys: ['backup'],
    sidebarGroupIds: ['backup'],
    resourcePrefixes: ['backup'],
    permissions: [PERMISSIONS.BACKUP_MANAGE, PERMISSIONS.SETTINGS_VIEW],
  },
  einstellungen: {
    key: 'einstellungen',
    catalogDisplayName: 'Einstellungen',
    labelKey: 'users.roleDrawer.groups.einstellungen',
    icon: 'SettingOutlined',
    menuKeys: ['settings', 'workingHours', 'license'],
    sidebarGroupIds: ['settings', 'license'],
    resourcePrefixes: ['settings', 'license', 'website', 'localization', 'receipttemplate'],
    permissions: [
      PERMISSIONS.SETTINGS_VIEW,
      PERMISSIONS.SETTINGS_MANAGE,
      PERMISSIONS.LICENSE_VIEW,
      PERMISSIONS.WEBSITE_MANAGE,
    ],
  },
  digitale_dienste: {
    key: 'digitale_dienste',
    catalogDisplayName: 'Digitale Dienste',
    labelKey: 'users.roleDrawer.groups.digitale_dienste',
    icon: 'GlobalOutlined',
    menuKeys: ['digitalServices'],
    sidebarGroupIds: ['settings', 'license'],
    resourcePrefixes: ['digital'],
    permissions: [
      PERMISSIONS.DIGITAL_VIEW,
      PERMISSIONS.DIGITAL_REQUEST,
      PERMISSIONS.DIGITAL_MANAGE,
      PERMISSIONS.DIGITAL_ORDERS_VIEW,
    ],
  },
  system: {
    key: 'system',
    catalogDisplayName: 'System',
    labelKey: 'users.roleDrawer.groups.system',
    icon: 'ToolOutlined',
    menuKeys: ['tenants', 'billing'],
    sidebarGroupIds: ['administration', 'license'],
    resourcePrefixes: ['system', 'tenant'],
    permissions: [PERMISSIONS.SYSTEM_CRITICAL, PERMISSIONS.TENANT_MANAGE],
  },
  sonstige: {
    key: 'sonstige',
    catalogDisplayName: 'Sonstige',
    labelKey: 'users.roleDrawer.groups.sonstige',
    icon: 'UnorderedListOutlined',
    menuKeys: [],
    sidebarGroupIds: [],
    resourcePrefixes: ['price', 'receipt'],
    permissions: [PERMISSIONS.PRICE_OVERRIDE, PERMISSIONS.RECEIPT_REPRINT],
  },
  other: {
    key: 'other',
    catalogDisplayName: 'Other',
    labelKey: 'users.roleDrawer.groups.other',
    icon: 'UnorderedListOutlined',
    menuKeys: [],
    sidebarGroupIds: [],
    resourcePrefixes: [],
    permissions: [],
  },
} as const satisfies Record<PermissionGroupKey, PermissionGroupDefinition>;

/** Sidebar-aligned display order (permission UI collapse / matrix). */
export const PERMISSION_GROUP_ORDER: readonly PermissionGroupKey[] = [
  'mitarbeiter',
  'kassenverwaltung',
  'bestellung_verkauf',
  'zahlung',
  'tagesabschluss',
  'rksv_finanzonline',
  'sortiment_preise',
  'lager',
  'kunden_vorteile',
  'audit_berichte',
  'backup_disaster_recovery',
  'einstellungen',
  'digitale_dienste',
  'system',
  'sonstige',
  'other',
] as const;

/**
 * Menu areas that intentionally have no dedicated permission catalog group
 * (hub / authenticated-only aggregators).
 */
export const MENU_AREAS_WITHOUT_PERMISSION_GROUP: ReadonlySet<MenuAreaKey> = new Set([
  'dashboard',
  'operations',
]);

/** Menu area → primary permission group (null = intentional hub). */
export const MENU_AREA_TO_PERMISSION_GROUP: Record<MenuAreaKey, PermissionGroupKey | null> = {
  dashboard: null,
  operations: null,
  tables: 'bestellung_verkauf',
  cashRegisters: 'kassenverwaltung',
  employees: 'mitarbeiter',
  shifts: 'kassenverwaltung',
  sales: 'bestellung_verkauf',
  tagesabschluss: 'tagesabschluss',
  rksv: 'rksv_finanzonline',
  finanzOnline: 'rksv_finanzonline',
  products: 'sortiment_preise',
  categories: 'sortiment_preise',
  customers: 'kunden_vorteile',
  benefits: 'kunden_vorteile',
  reports: 'audit_berichte',
  backup: 'backup_disaster_recovery',
  settings: 'einstellungen',
  workingHours: 'einstellungen',
  digitalServices: 'digitale_dienste',
  tenants: 'system',
  users: 'mitarbeiter',
  license: 'einstellungen',
  billing: 'system',
};

/** Permission-key overrides (mirrors backend PermissionKeyToGroupOverride). */
const PERMISSION_KEY_GROUP_OVERRIDES: Readonly<Record<string, PermissionGroupKey>> = {
  'settings.backup': 'backup_disaster_recovery',
};

const RESOURCE_PREFIX_TO_GROUP: ReadonlyMap<string, PermissionGroupKey> = (() => {
  const map = new Map<string, PermissionGroupKey>();
  for (const groupKey of PERMISSION_GROUP_ORDER) {
    const def = PERMISSION_GROUPS[groupKey];
    for (const prefix of def.resourcePrefixes) {
      map.set(prefix.toLowerCase(), groupKey);
    }
  }
  return map;
})();

export function getPermissionGroup(key: PermissionGroupKey): PermissionGroupDefinition {
  return PERMISSION_GROUPS[key];
}

export function listPermissionGroupsInOrder(): PermissionGroupDefinition[] {
  return PERMISSION_GROUP_ORDER.map((key) => PERMISSION_GROUPS[key]);
}

/** Groups that map to at least one sidebar shell group (for dynamic IA sync). */
export function listSidebarLinkedPermissionGroups(): PermissionGroupDefinition[] {
  return listPermissionGroupsInOrder().filter((g) => g.sidebarGroupIds.length > 0);
}

/**
 * Resolve catalog group slug for a permission key (resource.action).
 * Aligns with backend `GetGroupKeyForPermission`.
 */
export function resolvePermissionGroupSlugForPermissionKey(permissionKey: string): PermissionGroupKey {
  if (!permissionKey?.trim()) return 'other';
  const override = PERMISSION_KEY_GROUP_OVERRIDES[permissionKey];
  if (override) return override;
  const resource = permissionKey.split('.')[0]?.toLowerCase() ?? '';
  return RESOURCE_PREFIX_TO_GROUP.get(resource) ?? 'other';
}

export function resolvePermissionGroupForMenuArea(
  area: MenuAreaKey
): PermissionGroupKey | null {
  return MENU_AREA_TO_PERMISSION_GROUP[area];
}

/**
 * Menu areas that should have a group but mapping is missing / points at `other` incorrectly.
 * Hubs listed in {@link MENU_AREAS_WITHOUT_PERMISSION_GROUP} are excluded.
 */
export function listMenuAreasMissingPermissionGroup(): MenuAreaKey[] {
  return (Object.keys(MENU_AREA_TO_PERMISSION_GROUP) as MenuAreaKey[]).filter((area) => {
    if (MENU_AREAS_WITHOUT_PERMISSION_GROUP.has(area)) return false;
    const group = MENU_AREA_TO_PERMISSION_GROUP[area];
    return group === null || group === undefined;
  });
}

/**
 * Whether debug overlay / console should report menu↔group gaps.
 * Enable with `NEXT_PUBLIC_DEBUG_MENU_PERMISSION_GROUPS=true` or `?debugMenuPermissions=1`.
 */
export function isMenuPermissionGroupDebugEnabled(
  searchParams?: URLSearchParams | { get(name: string): string | null } | null
): boolean {
  if (process.env.NEXT_PUBLIC_DEBUG_MENU_PERMISSION_GROUPS === 'true') return true;
  if (searchParams?.get('debugMenuPermissions') === '1') return true;
  return false;
}

export type MenuPermissionGroupGap = {
  menuKey: string;
  reason: 'no_route_permission' | 'unclassified_other' | 'missing_menu_area_group';
  detail?: string;
};

/**
 * Classify a sidebar leaf path against route permission → group.
 * Used by validation script and sidebar debug overlay.
 */
export function classifyMenuLeafPermissionGroup(
  menuKey: string,
  routePermission: string | string[] | undefined
): { group: PermissionGroupKey | null; gap?: MenuPermissionGroupGap } {
  if (routePermission === undefined) {
    return {
      group: null,
      gap: { menuKey, reason: 'no_route_permission' },
    };
  }
  const required = Array.isArray(routePermission) ? routePermission : [routePermission];
  if (required.length === 0) {
    // ANY_AUTHENTICATED hub — no catalog group required
    return { group: null };
  }
  const groups = new Set(
    required.map((p) => resolvePermissionGroupSlugForPermissionKey(p)).filter(Boolean)
  );
  if (groups.size === 1 && groups.has('other')) {
    return {
      group: 'other',
      gap: {
        menuKey,
        reason: 'unclassified_other',
        detail: required.join(', '),
      },
    };
  }
  const primary = [...groups].find((g) => g !== 'other') ?? [...groups][0] ?? null;
  return { group: primary };
}

export type PermissionUiGroup<T extends { key?: string; group?: string | null }> = {
  slug: PermissionGroupKey | string;
  definition: PermissionGroupDefinition | undefined;
  items: T[];
};

/**
 * Bucket API permission catalog items into UI groups ordered by {@link PERMISSION_GROUP_ORDER}.
 */
export function buildPermissionUiGroupsFromCatalog<
  T extends { key?: string; group?: string | null },
>(catalog: readonly T[]): PermissionUiGroup<T>[] {
  const map = new Map<string, T[]>();
  for (const item of catalog) {
    const fromKey = item.key ? resolvePermissionGroupSlugForPermissionKey(item.key) : null;
    const fromGroupName = item.group?.trim()
      ? item.group
          .trim()
          .replace(/ & /g, '_')
          .replace(/ /g, '_')
          .toLowerCase()
      : null;
    const slug = fromKey && fromKey !== 'other' ? fromKey : fromGroupName || fromKey || 'other';
    if (!map.has(slug)) map.set(slug, []);
    map.get(slug)!.push(item);
  }

  const ordered: PermissionUiGroup<T>[] = [];
  const seen = new Set<string>();
  for (const key of PERMISSION_GROUP_ORDER) {
    const items = map.get(key);
    if (!items?.length) continue;
    ordered.push({
      slug: key,
      definition: PERMISSION_GROUPS[key],
      items,
    });
    seen.add(key);
  }
  for (const [slug, items] of map) {
    if (seen.has(slug) || !items.length) continue;
    ordered.push({
      slug,
      definition: undefined,
      items,
    });
  }
  return ordered;
}

/**
 * Sidebar shell sync: permission groups that drive top-level IA, for layout/debug.
 * Full menu tree remains `SIDEBAR_LAYOUT_ROWS` + RKSV plugin; this keeps group metadata aligned.
 */
export function getSidebarPermissionGroupSyncRows(): Array<{
  group: PermissionGroupDefinition;
  sidebarGroupIds: readonly SidebarGroupId[];
  menuKeys: readonly MenuAreaKey[];
}> {
  return listSidebarLinkedPermissionGroups().map((group) => ({
    group,
    sidebarGroupIds: group.sidebarGroupIds,
    menuKeys: group.menuKeys,
  }));
}
