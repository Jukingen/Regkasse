/**
 * Declarative license-lockdown menu IA for FA.
 *
 * This is **not** a parallel sidebar tree — labels render via i18n (`labelKey`)
 * through `adminSidebarRegistry`. Path filtering still runs in
 * `sidebarLicenseLockdown.ts` (read-only payments/backup/reports stay visible).
 *
 * Sketch keys (`dashboard`, `data-export`, …) map onto real FA routes below.
 */

import { ADMIN_SIDEBAR_GROUP_KEYS } from '@/shared/adminSidebarNavigation';

export type LockdownMenuIconName =
  | 'DashboardOutlined'
  | 'KeyOutlined'
  | 'SettingOutlined'
  | 'UserOutlined'
  | 'DownloadOutlined'
  | 'DeleteOutlined'
  | 'CustomerServiceOutlined'
  | 'CreditCardOutlined'
  | 'CloudOutlined'
  | 'BarChartOutlined'
  | 'TeamOutlined'
  | 'FileTextOutlined';

export type LockdownVisibleMenuEntry = {
  /** Stable IA key (also used by `useLicenseMenuVisibility`). */
  key: string;
  /** i18n key (typically `nav.*` — same as sidebar registry). */
  labelKey: string;
  /** Canonical FA route (must exist in the app router / registry). */
  path: string;
  icon: LockdownMenuIconName;
  /** Sidebar menu keys / paths that satisfy this IA entry when locked. */
  sidebarKeys: readonly string[];
};

/**
 * Core menus that must remain reachable in Locked/Archived
 * (renewal, GDPR export/closure, settings hub, dashboard).
 */
export const LOCKDOWN_VISIBLE_MENUS = [
  {
    key: 'dashboard',
    labelKey: 'nav.dashboard',
    path: '/dashboard',
    icon: 'DashboardOutlined',
    sidebarKeys: [ADMIN_SIDEBAR_GROUP_KEYS.dashboard, '/dashboard'],
  },
  {
    key: 'license',
    labelKey: 'nav.licenseManagement',
    path: '/license',
    icon: 'KeyOutlined',
    sidebarKeys: [
      ADMIN_SIDEBAR_GROUP_KEYS.license,
      '/license',
      '/license/dashboard',
      '/admin/license',
    ],
  },
  {
    key: 'settings',
    labelKey: 'nav.settings',
    path: '/settings',
    icon: 'SettingOutlined',
    sidebarKeys: [ADMIN_SIDEBAR_GROUP_KEYS.settings, '/settings', '/settings/account'],
  },
  {
    key: 'profile',
    labelKey: 'settings.changePassword.title',
    /** No dedicated `/profile` route — password change under settings. */
    path: '/settings/password',
    icon: 'UserOutlined',
    sidebarKeys: ['/settings', '/settings/password'],
  },
  {
    key: 'data-export',
    labelKey: 'nav.dataManagement',
    path: '/settings/data-management',
    icon: 'DownloadOutlined',
    sidebarKeys: ['/settings/data-management', '/settings/account'],
  },
  {
    key: 'account-closure',
    labelKey: 'nav.accountManagement',
    path: '/settings/account',
    icon: 'DeleteOutlined',
    sidebarKeys: ['/settings/account', '/settings/data-management'],
  },
  {
    key: 'support',
    labelKey: 'nav.licenseManagement',
    /** Support CTA lives on the license surfaces (no `/support` page). */
    path: '/license',
    icon: 'CustomerServiceOutlined',
    sidebarKeys: ['/license', '/admin/license'],
  },
] as const satisfies readonly LockdownVisibleMenuEntry[];

export type LockdownVisibleMenuKey = (typeof LOCKDOWN_VISIBLE_MENUS)[number]['key'];

/**
 * Additional read-only surfaces kept visible under lockdown
 * (beyond the core IA list). Write-oriented leaves under these groups
 * are still hidden by `sidebarLicenseLockdown`.
 */
export const LOCKDOWN_READONLY_EXTRA_MENUS = [
  {
    key: 'payments',
    labelKey: 'nav.payments',
    path: '/payments',
    icon: 'CreditCardOutlined',
    sidebarKeys: [
      ADMIN_SIDEBAR_GROUP_KEYS.salesTransactions,
      '/payments',
      '/receipts',
      '/invoices',
    ],
  },
  {
    key: 'backup',
    labelKey: 'nav.backupDisasterRecovery',
    path: '/backup',
    icon: 'CloudOutlined',
    sidebarKeys: [ADMIN_SIDEBAR_GROUP_KEYS.backup, '/backup', '/backup/runs', '/backup/audit'],
  },
  {
    key: 'reports',
    labelKey: 'nav.reports',
    path: '/reporting',
    icon: 'BarChartOutlined',
    sidebarKeys: [ADMIN_SIDEBAR_GROUP_KEYS.reports, '/reporting'],
  },
  {
    key: 'customers',
    labelKey: 'nav.customers',
    path: '/customers',
    icon: 'TeamOutlined',
    sidebarKeys: [ADMIN_SIDEBAR_GROUP_KEYS.customers, '/customers'],
  },
  {
    key: 'tenants',
    labelKey: 'nav.tenants',
    path: '/admin/tenants',
    icon: 'TeamOutlined',
    /** Visible but disabled when locked. */
    sidebarKeys: [ADMIN_SIDEBAR_GROUP_KEYS.admin, '/admin/tenants'],
  },
] as const satisfies readonly LockdownVisibleMenuEntry[];

/**
 * IA / domain keys that must not appear as writable hubs when locked.
 * Path-level enforcement remains in `sidebarLicenseLockdown` HIDDEN_* sets
 * (sketch listed payments/backup/reports here — those stay as read-only extras above).
 */
export const LOCKDOWN_HIDDEN_MENUS = [
  'users',
  'products',
  'catalog',
  'rksv',
  'tse',
  'special-receipts',
  'access',
  'digital',
  'tables',
  'cash-registers',
  'operations-write',
  'backup-config',
  'audit-write',
] as const;

export type LockdownHiddenMenuKey = (typeof LOCKDOWN_HIDDEN_MENUS)[number];

/** Alias → concrete sidebar keys (from core visible menus). */
export const LOCKDOWN_MENU_ALIAS_TO_KEYS: Record<
  LockdownVisibleMenuKey,
  readonly string[]
> = Object.fromEntries(
  LOCKDOWN_VISIBLE_MENUS.map((entry) => [entry.key, entry.sidebarKeys])
) as unknown as Record<LockdownVisibleMenuKey, readonly string[]>;

/** Paths that the core IA list requires to remain unhidden. */
export const LOCKDOWN_CORE_VISIBLE_PATHS: readonly string[] = Array.from(
  new Set(LOCKDOWN_VISIBLE_MENUS.flatMap((entry) => [entry.path, ...entry.sidebarKeys]))
);

/** Paths from read-only extras (payments, backup, …). */
export const LOCKDOWN_READONLY_EXTRA_PATHS: readonly string[] = Array.from(
  new Set(LOCKDOWN_READONLY_EXTRA_MENUS.flatMap((entry) => [entry.path, ...entry.sidebarKeys]))
);

/** @deprecated Prefer `LOCKDOWN_VISIBLE_MENUS` — kept for sketch-shaped imports. */
export const LOCKDOWN_VISIBLE_MENU_KEYS = LOCKDOWN_VISIBLE_MENUS.map((m) => m.key);

/**
 * Human-readable policy summary for docs/tests (not a parallel menu tree).
 */
export const LICENSE_LOCKDOWN_MENU_POLICY = {
  dashboard: { visibleWhenLocked: true },
  license: { visibleWhenLocked: true },
  settings: { visibleWhenLocked: true },
  profile: { visibleWhenLocked: true },
  'data-export': { visibleWhenLocked: true },
  'account-closure': { visibleWhenLocked: true },
  support: { visibleWhenLocked: true },
  tenants: { visibleWhenLocked: true, disabledWhenLocked: true },
  users: { visibleWhenLocked: false },
  products: { visibleWhenLocked: false },
  customers: { visibleWhenLocked: true, readOnlyWhenLocked: true },
  payments: { visibleWhenLocked: true, readOnlyWhenLocked: true },
  receipts: { visibleWhenLocked: true, readOnlyWhenLocked: true },
  reports: { visibleWhenLocked: true, readOnlyWhenLocked: true },
  backup: { visibleWhenLocked: true, readOnlyWhenLocked: true },
  rksv: { visibleWhenLocked: false },
  tse: { visibleWhenLocked: false },
  billing: { visibleWhenLocked: true },
  audit: { visibleWhenLocked: true, readOnlyWhenLocked: true },
} as const;
