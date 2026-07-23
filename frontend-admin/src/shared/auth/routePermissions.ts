/**
 * Path → required permission(s) for route access (longest-prefix match).
 * Used by `PermissionRouteGuard`. Sidebar/menu visibility uses `menuPermissions.MENU_PERMISSION` separately;
 * every sidebar leaf must have matching entries here — enforced by `sidebarRouteCoverage` tests.
 *
 * Empty array = route requires at least one permission (no specific permission).
 * Array default = ANY (OR). Paths in {@link ROUTE_PERMISSIONS_REQUIRE_ALL} need EVERY listed permission (AND),
 * matching backend multi-`[HasPermission]` attributes.
 * Fail-closed: no permissions in token → deny unless migration flag is set.
 */
import { ANY_AUTHENTICATED_PERMISSION, AppPermissions, PERMISSIONS } from './permissions';

/**
 * Routes whose permission arrays are AND (backend requires all).
 * All other array entries in {@link ROUTE_PERMISSIONS} remain OR.
 */
export const ROUTE_PERMISSIONS_REQUIRE_ALL = new Set<string>([
  '/rksv/dep-export',
  '/admin/rksv/dep-export',
  '/rksv/integrity',
]);

export const ROUTE_PERMISSIONS: Record<string, string | string[]> = {
  '/dashboard': ANY_AUTHENTICATED_PERMISSION,
  '/403': ANY_AUTHENTICATED_PERMISSION,
  '/profile': ANY_AUTHENTICATED_PERMISSION,
  /** Self-service password change — any authenticated user (header menu). */
  '/settings/password': ANY_AUTHENTICATED_PERMISSION,
  '/products': PERMISSIONS.PRODUCT_VIEW,
  '/pricing-rules': PERMISSIONS.PRODUCT_VIEW,
  '/categories': PERMISSIONS.CATEGORY_VIEW,
  '/inventory': PERMISSIONS.INVENTORY_VIEW,
  '/modifier-groups': PERMISSIONS.PRODUCT_VIEW,
  '/invoices': PERMISSIONS.INVOICE_VIEW,
  '/orders': [PERMISSIONS.DIGITAL_ORDERS_VIEW, PERMISSIONS.ORDER_VIEW],
  '/orders/online': [PERMISSIONS.DIGITAL_ORDERS_VIEW, PERMISSIONS.ORDER_VIEW],
  /** Legacy alias → `/orders/online` */
  '/online-orders': [PERMISSIONS.DIGITAL_ORDERS_VIEW, PERMISSIONS.ORDER_VIEW],
  '/payments': PERMISSIONS.PAYMENT_VIEW,
  '/payments/trends': PERMISSIONS.PAYMENT_VIEW,
  '/payments/card-transactions': PERMISSIONS.PAYMENT_VIEW,
  '/admin/payments/card-transactions': PERMISSIONS.PAYMENT_VIEW,
  '/payments/storno-refund-audit': PERMISSIONS.PAYMENT_VIEW,
  /** Legacy alias → `/payments/storno-refund-audit` (must be registered or PermissionRouteGuard 403s before redirect). */
  '/storno': PERMISSIONS.PAYMENT_VIEW,
  '/admin/tse/offline-transactions': PERMISSIONS.PAYMENT_VIEW,
  /** Manager oversight — operational sales (receipts / payments filter pages). */
  '/receipts': PERMISSIONS.SALE_VIEW,
  '/vouchers/new': PERMISSIONS.VOUCHER_CREATE,
  '/vouchers': PERMISSIONS.VOUCHER_READ,
  /** Manager oversight — reporting pages with shared CashRegisterSelector. */
  '/reporting': PERMISSIONS.REPORT_VIEW,
  '/reporting/report-center': PERMISSIONS.REPORT_VIEW,
  '/reporting/staff': PERMISSIONS.REPORT_VIEW,
  '/reporting/activity-log': PERMISSIONS.AUDIT_VIEW,
  '/reporting/compliance': PERMISSIONS.REPORT_VIEW,
  '/reporting/tagesbericht': PERMISSIONS.REPORT_VIEW,
  '/reporting/monatsbericht': PERMISSIONS.REPORT_VIEW,
  '/reporting/jahresbericht': PERMISSIONS.REPORT_VIEW,
  /** Legacy alias → `/receipts` (must match target guard; SALE_VIEW not REPORT_VIEW). */
  '/sales': PERMISSIONS.SALE_VIEW,
  '/reports/daily-closing': PERMISSIONS.REPORT_VIEW,
  '/admin/reports': PERMISSIONS.REPORT_VIEW,
  '/admin/reports/user-activity': PERMISSIONS.REPORT_VIEW,
  '/tables': PERMISSIONS.TABLE_VIEW,
  '/kassenverwaltung': AppPermissions.CashRegisterManage,
  /** Shift overview — requires shift.view (Manager also has cash_register.view for register list API). */
  '/shifts': PERMISSIONS.SHIFT_VIEW,
  /**
   * Staff hub (Manager read-only oversight). No `staff.*` keys in backend catalog — map spec aliases:
   * | Spec route / concept        | App Router path      | JWT permission(s)        |
   * |-----------------------------|----------------------|--------------------------|
   * | staff.view (hub + list)     | /staff, /staff/list  | user.view                |
   * | staff.view_activity (drawer)| (no dedicated route)| user.view (+ report.view for compliance tab) |
   * | staff.view (performance)    | /staff/performance   | report.view              |
   * | staff.view (shifts)         | /staff/shifts        | shift.view               |
   * | staff.manage (CRUD)         | /admin/users         | user.manage (not /staff) |
   * Hub entry: any of list / performance / shifts permissions (secondary nav filters per tab).
   */
  '/staff': [PERMISSIONS.USER_VIEW, PERMISSIONS.REPORT_VIEW, PERMISSIONS.SHIFT_VIEW],
  '/staff/list': PERMISSIONS.USER_VIEW,
  '/staff/performance': PERMISSIONS.REPORT_VIEW,
  '/staff/shifts': PERMISSIONS.SHIFT_VIEW,
  '/tagesabschluss': [PERMISSIONS.DAILY_CLOSING_VIEW],
  '/tagesabschluss/execute': [PERMISSIONS.DAILY_CLOSING_EXECUTE],
  /** Manager activity log (tenant-scoped audit rows; platform operators hidden on API). Spec alias: activity.view → audit.view. */
  '/audit-logs/activity': PERMISSIONS.AUDIT_VIEW,
  '/audit-logs/operations': PERMISSIONS.AUDIT_VIEW,
  /** Staff performance report linked from activity hub. */
  '/audit-logs/staff': PERMISSIONS.REPORT_VIEW,
  '/audit-logs': PERMISSIONS.AUDIT_VIEW,
  '/admin/audit/fiscal-exports': PERMISSIONS.AUDIT_VIEW,
  '/admin/download-history': PERMISSIONS.AUDIT_VIEW,
  '/admin/download-history/analytics': PERMISSIONS.AUDIT_VIEW,
  '/users': PERMISSIONS.USER_VIEW,
  '/admin/users': PERMISSIONS.USER_VIEW,
  '/admin/access': PERMISSIONS.USER_VIEW,
  /** Role CRUD + permission editor — Super Admin only (role.manage). */
  '/admin/access/roles': PERMISSIONS.ROLE_MANAGE,
  /** Read-only matrix — Manager may view (role.view). */
  '/admin/access/matrix': PERMISSIONS.ROLE_VIEW,
  /** Permission change history — audit.view. */
  '/admin/access/permission-history': PERMISSIONS.AUDIT_VIEW,
  /** Pending temporary permission requests — Super Admin. */
  '/admin/access/permission-requests': PERMISSIONS.SYSTEM_CRITICAL,
  /** Permission packages catalog. */
  '/admin/access/permission-packages': PERMISSIONS.ROLE_VIEW,
  /** Permission config backups — Super Admin. */
  '/admin/access/permission-backups': PERMISSIONS.SYSTEM_CRITICAL,
  /** Permission usage analytics — Super Admin. */
  '/admin/access/permission-stats': PERMISSIONS.SYSTEM_CRITICAL,
  '/settings': PERMISSIONS.SETTINGS_VIEW,
  /** Super Admin / settings.manage — firm-wide fiscal master data. */
  '/settings/company': PERMISSIONS.SETTINGS_MANAGE,
  '/settings/working-hours': PERMISSIONS.SETTINGS_VIEW,
  '/settings/website': [
    PERMISSIONS.DIGITAL_VIEW,
    PERMISSIONS.DIGITAL_PREVIEW,
    PERMISSIONS.DIGITAL_REQUEST,
    PERMISSIONS.DIGITAL_CREATE,
    PERMISSIONS.WEBSITE_MANAGE,
  ],
  '/settings/digital': [
    PERMISSIONS.DIGITAL_VIEW,
    PERMISSIONS.DIGITAL_REQUEST,
    PERMISSIONS.WEBSITE_MANAGE,
  ],
  '/digital/customer-portal': [
    PERMISSIONS.DIGITAL_VIEW,
    PERMISSIONS.DIGITAL_REQUEST,
    PERMISSIONS.WEBSITE_MANAGE,
  ],
  '/settings/session': PERMISSIONS.SETTINGS_VIEW,
  '/settings/sessions': PERMISSIONS.SETTINGS_VIEW,
  /** Super Admin / settings.manage — offline limits and toggles. */
  '/settings/offline': PERMISSIONS.SETTINGS_MANAGE,
  '/settings/personalization': PERMISSIONS.SETTINGS_VIEW,
  /** Legacy redirect — canonical `/settings/personalization`. */
  '/settings/appearance': PERMISSIONS.SETTINGS_VIEW,
  '/settings/preferences': PERMISSIONS.SETTINGS_VIEW,
  '/settings/payment-methods': PERMISSIONS.SETTINGS_VIEW,
  /** Super Admin / settings.manage — Stripe/Mock gateway status + online checkout methods. */
  '/settings/payment': PERMISSIONS.SETTINGS_MANAGE,
  /** Super Admin / settings.manage — TSE defaults (legacy hub tab; deep-link route). */
  '/settings/tse': PERMISSIONS.SETTINGS_MANAGE,
  /** Super Admin / settings.manage — FinanzOnline credentials (legacy hub tab; deep-link route). */
  '/settings/finanzonline': PERMISSIONS.SETTINGS_MANAGE,
  /** Tenant backup schedule/trigger — backup.manage (Manager tenant scope; platform ops gated in UI). */
  '/settings/backup': PERMISSIONS.BACKUP_MANAGE,
  '/settings/data-management': PERMISSIONS.BACKUP_MANAGE,
  /** Legacy redirect — canonical `/backup/dashboard`. */
  '/settings/backup-dr': PERMISSIONS.SETTINGS_VIEW,
  /** Canonical backup & DR routes */
  '/backup': PERMISSIONS.SETTINGS_VIEW,
  '/backup/dashboard': PERMISSIONS.SETTINGS_VIEW,
  '/backup/performance': PERMISSIONS.SETTINGS_VIEW,
  '/backup/compliance': PERMISSIONS.SETTINGS_VIEW,
  '/backup/costs': PERMISSIONS.SETTINGS_VIEW,
  '/backup/restore-history': PERMISSIONS.SETTINGS_VIEW,
  '/backup/runs': PERMISSIONS.SETTINGS_VIEW,
  '/backup/configuration': PERMISSIONS.SETTINGS_VIEW,
  '/backup/configuration/schedule': PERMISSIONS.BACKUP_MANAGE,
  '/backup/configuration/platform': PERMISSIONS.SETTINGS_MANAGE,
  '/backup/audit': PERMISSIONS.SETTINGS_VIEW,
  /** Legacy redirects */
  '/backup/config': PERMISSIONS.SETTINGS_VIEW,
  '/backup/logs': PERMISSIONS.SETTINGS_VIEW,
  /** Legacy redirect — canonical `/backup/runs`. */
  '/admin/backup': PERMISSIONS.SETTINGS_VIEW,
  '/settings/development-mode': PERMISSIONS.SYSTEM_CRITICAL,
  '/admin/system/time-sync': PERMISSIONS.SETTINGS_MANAGE,
  '/admin/license': [PERMISSIONS.LICENSE_MANAGE, PERMISSIONS.SETTINGS_MANAGE],
  '/admin/license/test': PERMISSIONS.SYSTEM_CRITICAL,
  '/admin/license/debug': PERMISSIONS.SYSTEM_CRITICAL,
  /**
   * Platform Super Admin landing (`SuperAdminModeBanner` → Mandant auswählen).
   * Must be mapped: fail-closed guard returns 403 when a path is missing from this table.
   * Longer `/admin/*` keys still win via longest-prefix match.
   */
  '/admin': PERMISSIONS.SYSTEM_CRITICAL,
  '/admin/tenants': PERMISSIONS.SYSTEM_CRITICAL,
  '/admin/tenants/create': PERMISSIONS.SYSTEM_CRITICAL,
  '/admin/approvals': PERMISSIONS.SYSTEM_CRITICAL,
  '/admin/maintenance': PERMISSIONS.SYSTEM_CRITICAL,
  '/admin/data-management': PERMISSIONS.SYSTEM_CRITICAL,
  /**
   * Super Admin tenant tooling (`/tenant/{id}/customize|domain|…`).
   * Manager-allowed deep links under `/tenant/{id}/…` are resolved via
   * {@link getTenantScopedRoutePermission} (longest static prefix alone would block them).
   */
  '/tenant': PERMISSIONS.SYSTEM_CRITICAL,
  '/admin/errors': PERMISSIONS.SYSTEM_CRITICAL,
  '/admin/licenses': PERMISSIONS.LICENSE_VIEW,
  /** Cross-tenant platform register list — not tenant `/kassenverwaltung` (cash_register.manage). */
  '/admin/cash-registers': PERMISSIONS.SYSTEM_CRITICAL,
  // Billing routes (Super Admin only)
  '/admin/billing': [PERMISSIONS.SYSTEM_CRITICAL],
  '/admin/billing/sales': [PERMISSIONS.SYSTEM_CRITICAL],
  '/admin/billing/sales/new': [PERMISSIONS.SYSTEM_CRITICAL],
  /** Sale detail (`/admin/billing/sales/{id}`) — longest-prefix match via `/admin/billing/sales`. */
  '/admin/billing/stats': [PERMISSIONS.SYSTEM_CRITICAL],
  '/billing/digital': [PERMISSIONS.DIGITAL_MANAGE, PERMISSIONS.SYSTEM_CRITICAL],
  '/admin/digital': [
    PERMISSIONS.DIGITAL_MANAGE,
    PERMISSIONS.DIGITAL_ACTIVATE,
    PERMISSIONS.DIGITAL_PRICING_MANAGE,
    PERMISSIONS.SYSTEM_CRITICAL,
  ],
  '/admin/digital/requests': [PERMISSIONS.DIGITAL_MANAGE, PERMISSIONS.SYSTEM_CRITICAL],
  '/admin/feedback': [PERMISSIONS.SYSTEM_CRITICAL],
  '/admin/monitoring': [PERMISSIONS.SYSTEM_CRITICAL],
  '/admin/risk-dashboard': [PERMISSIONS.SYSTEM_CRITICAL],
  '/admin/tse-management': [PERMISSIONS.SYSTEM_CRITICAL],
  '/admin/tse/failover': [PERMISSIONS.SYSTEM_CRITICAL],
  '/receipt-templates': PERMISSIONS.RECEIPT_TEMPLATE_VIEW,
  '/receipt-generate': PERMISSIONS.RECEIPT_TEMPLATE_VIEW,
  '/customers': PERMISSIONS.CUSTOMER_VIEW,
  '/operations-center': [
    PERMISSIONS.SALE_VIEW,
    PERMISSIONS.TSE_SIGN,
    PERMISSIONS.RECEIPT_REPRINT,
    PERMISSIONS.REPORT_EXPORT,
  ],
  '/rksv': PERMISSIONS.FINANZONLINE_MANAGE,
  /** Explicit hub alias path (same access as `/rksv`; avoids prefix-only drift in audits). */
  '/rksv/operations': PERMISSIONS.FINANZONLINE_MANAGE,
  /** Sidebar-only virtual keys: same access as Sonderbelege page (query focus deep links). */
  '/rksv/sb/startbeleg': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/sb/monatsbeleg': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/sb/jahresbeleg': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/sb/nullbeleg': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/sb/schlussbeleg': PERMISSIONS.FINANZONLINE_MANAGE,
  /** Sidebar-only: Demo-Modus test tools on Sonderbelege (Super Admin / system.critical). */
  '/rksv/sb/test-helper': PERMISSIONS.SYSTEM_CRITICAL,
  '/rksv/sonderbelege': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/status': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/cmc-certificate': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/verifications': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/finanz-online-operations': PERMISSIONS.FINANZONLINE_VIEW,
  '/rksv/finanz-online-outbox': PERMISSIONS.FINANZONLINE_VIEW,
  '/rksv/finanz-online-queue': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/fiscal-export-diagnostics': PERMISSIONS.REPORT_EXPORT,
  '/rksv/dep-export': [PERMISSIONS.REPORT_EXPORT, PERMISSIONS.AUDIT_VIEW],
  '/admin/rksv/dep-export': [PERMISSIONS.REPORT_EXPORT, PERMISSIONS.AUDIT_VIEW],
  '/admin/rksv/signature-verify': PERMISSIONS.AUDIT_VIEW,
  '/rksv/replay-batch': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/incident': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/payload-hash-conflicts': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/offline-intent-coverage': PERMISSIONS.REPORT_EXPORT,
  '/rksv/integrity': [PERMISSIONS.AUDIT_VIEW, PERMISSIONS.FINANZONLINE_MANAGE],
  '/rksv/compliance': PERMISSIONS.AUDIT_VIEW,
  '/rksv/signature-chain': PERMISSIONS.AUDIT_VIEW,
  '/rksv/belegcheck': PERMISSIONS.PAYMENT_VIEW,
  '/rksv/offline-orders': PERMISSIONS.PAYMENT_VIEW,
  '/benefit-definitions': PERMISSIONS.BENEFIT_VIEW,
  '/benefit-assignments': PERMISSIONS.BENEFIT_VIEW,
};

/** Sorted route prefixes for longest-match lookup (e.g. /receipt-templates/123 → /receipt-templates). */
const ROUTE_KEYS_SORTED = (Object.keys(ROUTE_PERMISSIONS) as string[]).sort(
  (a, b) => b.length - a.length
);

/**
 * `/tenant/{tenantId}/{leaf}` routes Managers may open (ambient or Super Admin tenant context).
 * Checked before the `/tenant` → system.critical prefix so Mandanten are not blocked.
 */
const TENANT_SCOPED_LEAF_PERMISSIONS: Record<string, string | string[]> = {
  digital: [
    PERMISSIONS.DIGITAL_VIEW,
    PERMISSIONS.DIGITAL_PREVIEW,
    PERMISSIONS.DIGITAL_REQUEST,
    PERMISSIONS.DIGITAL_CREATE,
    PERMISSIONS.WEBSITE_MANAGE,
    PERMISSIONS.SYSTEM_CRITICAL,
  ],
  'website-preview': [
    PERMISSIONS.DIGITAL_VIEW,
    PERMISSIONS.DIGITAL_PREVIEW,
    PERMISSIONS.DIGITAL_REQUEST,
    PERMISSIONS.DIGITAL_CREATE,
    PERMISSIONS.WEBSITE_MANAGE,
    PERMISSIONS.SYSTEM_CRITICAL,
  ],
  orders: [PERMISSIONS.DIGITAL_ORDERS_VIEW, PERMISSIONS.ORDER_VIEW, PERMISSIONS.SYSTEM_CRITICAL],
  'data-management': [PERMISSIONS.BACKUP_MANAGE, PERMISSIONS.SYSTEM_CRITICAL],
};

/** Match `/tenant/{id}/{leaf}` Manager deep links; otherwise undefined. */
export function getTenantScopedRoutePermission(pathname: string): string | string[] | undefined {
  const parts = (pathname.replace(/\/$/, '') || '/').split('/').filter(Boolean);
  if (parts.length !== 3 || parts[0] !== 'tenant') return undefined;
  const leaf = parts[2];
  if (!leaf || !(leaf in TENANT_SCOPED_LEAF_PERMISSIONS)) return undefined;
  return TENANT_SCOPED_LEAF_PERMISSIONS[leaf];
}

/**
 * Returns required permission(s) for path. Exact match first, then tenant-scoped
 * `/tenant/{id}/…` leaves, then longest prefix match.
 * Protects dynamic segments (e.g. /receipt-templates/[id], /receipts/[receiptId]).
 */
export function getRequiredPermissionForPath(pathname: string): string | string[] | undefined {
  const normalized = pathname.replace(/\/$/, '') || '/';
  if (ROUTE_PERMISSIONS[normalized] !== undefined) return ROUTE_PERMISSIONS[normalized];
  const tenantScoped = getTenantScopedRoutePermission(normalized);
  if (tenantScoped !== undefined) return tenantScoped;
  for (const key of ROUTE_KEYS_SORTED) {
    if (normalized === key || normalized.startsWith(key + '/')) return ROUTE_PERMISSIONS[key];
  }
  return undefined;
}

/** True when the path's permission array must be satisfied with AND (all), not OR (any). */
export function pathRequiresAllPermissions(pathname: string): boolean {
  const normalized = pathname.replace(/\/$/, '') || '/';
  if (ROUTE_PERMISSIONS_REQUIRE_ALL.has(normalized)) return true;
  for (const key of ROUTE_PERMISSIONS_REQUIRE_ALL) {
    if (normalized === key || normalized.startsWith(key + '/')) return true;
  }
  return false;
}

/** Shared rule for {@link PermissionRouteGuard} and {@link canAccessPath}. */
export function permissionsSatisfyRoute(
  pathname: string,
  permissions: string[],
  required: string | string[]
): boolean {
  const arr = Array.isArray(required) ? required : [required];
  if (arr.length === 0) return permissions.length > 0;
  if (!permissions.length) return false;
  return pathRequiresAllPermissions(pathname)
    ? arr.every((p) => permissions.includes(p))
    : arr.some((p) => permissions.includes(p));
}
