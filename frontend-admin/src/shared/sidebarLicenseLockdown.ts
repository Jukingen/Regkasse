import type { MenuProps } from 'antd';

import type { LicenseLifecycleUiState } from '@/hooks/useLicenseStatus';
import { ADMIN_SIDEBAR_GROUP_KEYS } from '@/shared/adminSidebarNavigation';
import {
  LICENSE_LOCKDOWN_MENU_POLICY,
  LOCKDOWN_CORE_VISIBLE_PATHS,
  LOCKDOWN_READONLY_EXTRA_PATHS,
} from '@/shared/licenseMenuConfig';

/**
 * Sidebar visibility under mandant license lockdown (Locked / Archived).
 *
 * Menu tree source of truth remains `adminSidebarRegistry` + `filterSidebarMenuItems`.
 * Declarative IA allowlist: `licenseMenuConfig.ts`.
 * This module hides write-oriented leaves/groups and optionally disables navigation.
 *
 * Sketch mapping (user IA → FA keys):
 * - license / dashboard → always visible
 * - tenants → visible, disabled when locked
 * - users / products / rksv → hidden when locked
 * - payments / backup → visible (read-only pages; write config leaves hidden)
 */

export type SidebarLicenseLockdownState = Extract<
  LicenseLifecycleUiState,
  'Locked' | 'Archived'
>;

function isExactOrChild(key: string, root: string): boolean {
  return key === root || key.startsWith(`${root}/`);
}

function matchesAnyPrefix(key: string, prefixes: readonly string[]): boolean {
  return prefixes.some((prefix) => isExactOrChild(key, prefix));
}

/** Always keep (license renewal + read overview + GDPR + read-only extras). */
const ALWAYS_VISIBLE_EXACT = new Set<string>([
  ...LOCKDOWN_CORE_VISIBLE_PATHS,
  ...LOCKDOWN_READONLY_EXTRA_PATHS,
  ADMIN_SIDEBAR_GROUP_KEYS.dashboard,
  ADMIN_SIDEBAR_GROUP_KEYS.license,
  ADMIN_SIDEBAR_GROUP_KEYS.reports,
  ADMIN_SIDEBAR_GROUP_KEYS.backup,
  ADMIN_SIDEBAR_GROUP_KEYS.salesTransactions,
  ADMIN_SIDEBAR_GROUP_KEYS.settings,
  ADMIN_SIDEBAR_GROUP_KEYS.customers,
  ADMIN_SIDEBAR_GROUP_KEYS.admin,
  '/dashboard',
  '/license',
  '/license/dashboard',
  '/admin/license',
  '/admin/license-management',
  '/admin/license/grace-period',
  '/admin/licenses',
  '/admin/billing',
  '/reporting',
  '/reporting/report-center',
  '/reporting/steuerberichte',
  '/reporting/compliance',
  '/reporting/staff',
  '/audit-logs/activity',
  '/admin/reports/user-activity',
  '/backup',
  '/backup/runs',
  '/backup/audit',
  '/payments',
  '/payments/trends',
  '/payments/storno-refund-audit',
  '/admin/payments/card-transactions',
  '/receipts',
  '/invoices',
  '/orders/online',
  '/customers',
  '/settings',
  '/settings/data-management',
  '/settings/account',
  '/settings/password',
  '/tenant/dashboard',
  '/tenant/portal',
  '/tenant/invoices',
  '/tenant/support',
  '/admin/tenants',
  '/admin/data-management',
]);

const ALWAYS_VISIBLE_PREFIXES = [
  '/license',
  '/admin/license',
  '/admin/license-management',
  '/admin/licenses',
  '/admin/billing',
  '/billing/digital',
  '/dashboard',
  '/reporting',
  '/backup',
  '/payments',
  '/receipts',
  '/invoices',
  '/customers',
  '/settings/data-management',
  '/settings/account',
  '/settings/password',
  '/tenant/',
] as const;

/**
 * Write-oriented menu keys/prefixes hidden when license is Locked/Archived.
 * Prefer hiding groups that only contain write leaves (catalog, RKSV, cash-register ops).
 */
const HIDDEN_EXACT = new Set<string>([
  ADMIN_SIDEBAR_GROUP_KEYS.catalog,
  ADMIN_SIDEBAR_GROUP_KEYS.rksv,
  ADMIN_SIDEBAR_GROUP_KEYS.specialReceipts,
  ADMIN_SIDEBAR_GROUP_KEYS.rksvTools,
  ADMIN_SIDEBAR_GROUP_KEYS.accessArea,
  ADMIN_SIDEBAR_GROUP_KEYS.digitalServices,
  ADMIN_SIDEBAR_GROUP_KEYS.backupConfig,
  '/products',
  '/categories',
  '/modifier-groups',
  '/pricing-rules',
  '/inventory',
  '/kassenverwaltung',
  '/tables',
  '/shifts',
  '/staff',
  '/tagesabschluss',
  '/operations-center',
  '/vouchers',
  '/receipt-templates',
  '/receipt-generate',
  '/benefit-definitions',
  '/benefit-assignments',
  '/admin/users',
  '/admin/access',
  '/admin/cash-registers',
  '/rksv',
  '/settings/company',
  '/settings/working-hours',
  '/settings/tax-groups',
  '/settings/tax-history',
  '/settings/website',
  '/settings/digital',
  '/digital/customer-portal',
  '/settings/tse',
  '/settings/finanzonline',
  '/settings/session',
  '/settings/sessions',
  '/settings/offline',
  '/settings/personalization',
  '/settings/preferences',
  '/settings/payment-methods',
  '/settings/payment',
  '/settings/development-mode',
  '/backup/configuration/schedule',
  '/backup/configuration/platform',
]);

const HIDDEN_PREFIXES = [
  '/products',
  '/categories',
  '/modifier-groups',
  '/pricing-rules',
  '/inventory',
  '/kassenverwaltung',
  '/tables',
  '/shifts',
  '/staff',
  '/tagesabschluss',
  '/vouchers',
  '/receipt-templates',
  '/receipt-generate',
  '/admin/users',
  '/admin/access',
  '/admin/cash-registers',
  '/admin/tse',
  '/admin/rksv',
  '/admin/digital',
  '/rksv',
  '/settings/company',
  '/settings/working-hours',
  '/settings/tax-groups',
  '/settings/tax-history',
  '/settings/website',
  '/settings/digital',
  '/digital/customer-portal',
  '/settings/tse',
  '/settings/finanzonline',
  '/settings/session',
  '/settings/sessions',
  '/settings/offline',
  '/settings/personalization',
  '/settings/preferences',
  '/settings/payment-methods',
  '/settings/payment',
  '/settings/development-mode',
  '/backup/configuration',
] as const;

/** Visible but not navigable (e.g. Mandanten list still shown as context). */
const DISABLED_EXACT = new Set<string>(['/admin/tenants']);

const DISABLED_PREFIXES = ['/admin/tenants'] as const;

export function isLicenseLockdownSidebarActive(
  state: LicenseLifecycleUiState | null | undefined
): state is SidebarLicenseLockdownState {
  return state === 'Locked' || state === 'Archived';
}

export function shouldHideSidebarKeyForLicenseLockdown(key: string): boolean {
  if (ALWAYS_VISIBLE_EXACT.has(key) || matchesAnyPrefix(key, ALWAYS_VISIBLE_PREFIXES)) {
    return false;
  }
  if (HIDDEN_EXACT.has(key) || matchesAnyPrefix(key, HIDDEN_PREFIXES)) {
    return true;
  }
  // Structural operations group stays if read-only sales children remain.
  if (key === ADMIN_SIDEBAR_GROUP_KEYS.operations) {
    return false;
  }
  return false;
}

export function shouldDisableSidebarKeyForLicenseLockdown(key: string): boolean {
  return DISABLED_EXACT.has(key) || matchesAnyPrefix(key, DISABLED_PREFIXES);
}

/**
 * Hide write-oriented sidebar entries when mandant license is Locked/Archived.
 * Super Admin bypasses (support / unlock flows).
 */
export function filterSidebarMenuItemsForLicenseLockdown(
  items: MenuProps['items'] | undefined,
  options: {
    licenseState: LicenseLifecycleUiState | null | undefined;
    isSuperAdmin: boolean;
  }
): MenuProps['items'] {
  if (!items?.length) return items;
  if (options.isSuperAdmin || !isLicenseLockdownSidebarActive(options.licenseState)) {
    return items;
  }

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
      disabled?: boolean;
      [k: string]: unknown;
    };

    const key = typeof node.key === 'string' ? node.key : '';

    if (key && shouldHideSidebarKeyForLicenseLockdown(key)) {
      continue;
    }

    if (node.children && node.children.length > 0) {
      const filteredChildren = filterSidebarMenuItemsForLicenseLockdown(node.children, options);
      if (!filteredChildren?.length) continue;
      const disabled =
        Boolean(node.disabled) ||
        (key ? shouldDisableSidebarKeyForLicenseLockdown(key) : false);
      result.push({
        ...it,
        children: filteredChildren,
        ...(disabled ? { disabled: true } : {}),
      } as NonNullable<MenuProps['items']>[number]);
      continue;
    }

    if (key && shouldDisableSidebarKeyForLicenseLockdown(key)) {
      result.push({ ...it, disabled: true } as NonNullable<MenuProps['items']>[number]);
      continue;
    }

    result.push(it);
  }

  return result;
}

/**
 * Human-readable policy summary for docs/tests (not a parallel menu tree).
 * @see licenseMenuConfig.ts
 * @see adminSidebarRegistry
 */
export const SIDEBAR_LICENSE_LOCKDOWN_MENU_POLICY = LICENSE_LOCKDOWN_MENU_POLICY;
