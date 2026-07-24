/**
 * Single source of truth for admin sidebar IA (non-RKSV leaves + shell layout).
 * RKSV subtree is attached via `registerRksvSidebar` in `composeAdminSidebarData`.
 *
 * Permission keys for menu filtering match `MENU_PERMISSION` map keys (= `menuKey` for leaves).
 * Primary IA areas pull `permission` / `permissionGroup` from `MENU_PERMISSION_MAP`.
 */
import {
  type RksvSidebarRegistryAttachment,
  registerRksvSidebar,
} from '@/features/rksv/sidebarPlugin';
import { ADMIN_SIDEBAR_GROUP_KEYS } from '@/shared/adminSidebarNavigation';
import {
  ANY_AUTHENTICATED_PERMISSION,
  PERMISSIONS,
} from '@/shared/auth/permissions';
import {
  logMenuPermissionMapWarnings,
  sidebarFieldsFromMenuMap,
  validateMenuPermissions,
  type MenuPermissionMapKey,
} from '@/shared/auth/menuPermissionMapping';
import type { PermissionGroupKey } from '@/shared/auth/permissionGroupRegistry';
import { FISCAL_RKSV_CLOSING_VIRTUAL_MENU_KEYS } from '@/shared/fiscalRksvClosingSidebar';
import type { RksvMenuGroup } from '@/shared/rksvMenuModel';
import { collectRksvMenuLeafKeys } from '@/shared/rksvMenuModel';

export type SidebarGroupId =
  | 'dashboard'
  | 'license'
  | 'operations'
  | 'rksv'
  | 'catalog'
  | 'customers'
  | 'reports'
  | 'backup'
  | 'settings'
  | 'administration';

/** Keys into `SIDEBAR_ICON_COMPONENTS` in `buildAdminSidebar.tsx`. */
export type SidebarIconToken =
  | 'ThunderboltOutlined'
  | 'ShoppingCartOutlined'
  | 'AppstoreOutlined'
  | 'UsergroupAddOutlined'
  | 'LineChartOutlined'
  | 'AuditOutlined'
  | 'ToolOutlined'
  | 'DashboardOutlined'
  | 'PieChartOutlined'
  | 'FundOutlined'
  | 'TeamOutlined'
  | 'FileTextOutlined'
  | 'FileDoneOutlined'
  | 'BarChartOutlined'
  | 'AreaChartOutlined'
  | 'ControlOutlined'
  | 'TableOutlined'
  | 'CalendarOutlined'
  | 'FileSearchOutlined'
  | 'CreditCardOutlined'
  | 'SnippetsOutlined'
  | 'EyeOutlined'
  | 'ShoppingOutlined'
  | 'FolderOutlined'
  | 'GroupOutlined'
  | 'TagOutlined'
  | 'InboxOutlined'
  | 'UserOutlined'
  | 'GiftOutlined'
  | 'SafetyCertificateOutlined'
  | 'SafetyOutlined'
  | 'SwapOutlined'
  | 'ClusterOutlined'
  | 'AlertOutlined'
  | 'SettingOutlined'
  | 'ShopOutlined'
  | 'GlobalOutlined'
  | 'BgColorsOutlined'
  | 'CloudServerOutlined'
  | 'ExperimentOutlined'
  | 'WalletOutlined'
  | 'ClockCircleOutlined'
  | 'LaptopOutlined'
  | 'CloudDownloadOutlined'
  | 'CloudSyncOutlined'
  | 'DatabaseOutlined'
  | 'HistoryOutlined'
  | 'KeyOutlined'
  | 'ApartmentOutlined'
  | 'DisconnectOutlined'
  | 'UnorderedListOutlined'
  | 'BugOutlined'
  | 'ApiOutlined';

export type SidebarNavCatalogItem = {
  /** Stable id for tests and layout references */
  id: string;
  /** Ant Design Menu item key (must match MENU_PERMISSION / route guard prefix keys) */
  menuKey: string;
  href: string;
  labelKey: string;
  icon?: SidebarIconToken;
  /** Required permission(s) for sidebar visibility (must match `ROUTE_PERMISSIONS[menuKey]`). */
  permission?: string | string[];
  /**
   * Logical IA key from `MENU_PERMISSION_MAP` when this leaf is the area primary.
   * Prefer setting via `sidebarFieldsFromMenuMap(area)`.
   */
  menuArea?: MenuPermissionMapKey;
  /** Catalog permission group slug (from `MENU_PERMISSION_MAP` when wired). */
  permissionGroup?: PermissionGroupKey | null;
  /** When true, route is registered but omitted from sidebar leaves (deep-link only). */
  sidebarHidden?: boolean;
  /** When true, leaf is shown only when `NODE_ENV === 'development'`. */
  developmentOnly?: boolean;
};

/**
 * Flat catalog: every non-RKSV routable sidebar leaf (and nested subgroup leaves).
 */
export const SIDEBAR_NAV_ITEM_CATALOG: Record<string, SidebarNavCatalogItem> = {
  operationsCenter: {
    id: 'operationsCenter',
    menuKey: '/operations-center',
    href: '/operations-center',
    labelKey: 'nav.operationsCenter',
    icon: 'ControlOutlined',
    ...sidebarFieldsFromMenuMap('operations'),
  },
  tables: {
    id: 'tables',
    menuKey: '/tables',
    href: '/tables',
    labelKey: 'nav.tables',
    icon: 'TableOutlined',
    ...sidebarFieldsFromMenuMap('tables'),
  },
  kassenverwaltung: {
    id: 'kassenverwaltung',
    menuKey: '/kassenverwaltung',
    href: '/kassenverwaltung',
    labelKey: 'nav.cashRegisters',
    icon: 'ShopOutlined',
    ...sidebarFieldsFromMenuMap('cashRegisters'),
  },
  shiftsOverview: {
    id: 'shiftsOverview',
    menuKey: '/shifts',
    href: '/shifts',
    labelKey: 'nav.shifts',
    icon: 'ClockCircleOutlined',
    ...sidebarFieldsFromMenuMap('shifts'),
  },
  staffHub: {
    id: 'staffHub',
    menuKey: '/staff',
    href: '/staff',
    labelKey: 'nav.staff',
    icon: 'TeamOutlined',
    ...sidebarFieldsFromMenuMap('employees'),
  },
  tagesabschluss: {
    id: 'tagesabschluss',
    menuKey: '/tagesabschluss',
    href: '/tagesabschluss',
    labelKey: 'nav.dailyClosing',
    icon: 'CalendarOutlined',
    ...sidebarFieldsFromMenuMap('tagesabschluss'),
  },
  receipts: {
    id: 'receipts',
    menuKey: '/receipts',
    href: '/receipts',
    labelKey: 'nav.receipts',
    icon: 'FileSearchOutlined',
    ...sidebarFieldsFromMenuMap('sales'),
  },
  payments: {
    id: 'payments',
    menuKey: '/payments',
    href: '/payments',
    labelKey: 'nav.payments',
    icon: 'CreditCardOutlined',
    permission: PERMISSIONS.PAYMENT_VIEW,
  },
  stornoRefundAudit: {
    id: 'stornoRefundAudit',
    menuKey: '/payments/storno-refund-audit',
    href: '/payments/storno-refund-audit',
    labelKey: 'nav.cancellations',
    icon: 'AuditOutlined',
    permission: PERMISSIONS.PAYMENT_VIEW,
  },
  paymentTrends: {
    id: 'paymentTrends',
    menuKey: '/payments/trends',
    href: '/payments/trends',
    labelKey: 'nav.paymentTrends',
    icon: 'AreaChartOutlined',
    permission: PERMISSIONS.PAYMENT_VIEW,
  },
  cardTransactions: {
    id: 'cardTransactions',
    menuKey: '/admin/payments/card-transactions',
    href: '/admin/payments/card-transactions',
    labelKey: 'nav.cardTransactions',
    icon: 'CreditCardOutlined',
    permission: PERMISSIONS.PAYMENT_VIEW,
  },
  vouchers: {
    id: 'vouchers',
    menuKey: '/vouchers',
    href: '/vouchers',
    labelKey: 'nav.vouchers',
    icon: 'WalletOutlined',
    permission: PERMISSIONS.VOUCHER_READ,
  },
  invoices: {
    id: 'invoices',
    menuKey: '/invoices',
    href: '/invoices',
    labelKey: 'nav.invoices',
    icon: 'FileDoneOutlined',
    permission: PERMISSIONS.INVOICE_VIEW,
  },
  onlineOrders: {
    id: 'onlineOrders',
    menuKey: '/orders/online',
    href: '/orders/online',
    labelKey: 'nav.onlineOrders',
    icon: 'ShoppingOutlined',
    permission: [PERMISSIONS.DIGITAL_ORDERS_VIEW, PERMISSIONS.ORDER_VIEW],
  },
  receiptTemplates: {
    id: 'receiptTemplates',
    menuKey: '/receipt-templates',
    href: '/receipt-templates',
    labelKey: 'nav.receiptTemplates',
    icon: 'SnippetsOutlined',
    sidebarHidden: true,
  },
  receiptGenerate: {
    id: 'receiptGenerate',
    menuKey: '/receipt-generate',
    href: '/receipt-generate',
    labelKey: 'nav.receiptGenerate',
    icon: 'EyeOutlined',
    sidebarHidden: true,
  },
  products: {
    id: 'products',
    menuKey: '/products',
    href: '/products',
    labelKey: 'nav.products',
    icon: 'ShoppingOutlined',
    ...sidebarFieldsFromMenuMap('products'),
  },
  categories: {
    id: 'categories',
    menuKey: '/categories',
    href: '/categories',
    labelKey: 'nav.categories',
    icon: 'FolderOutlined',
    ...sidebarFieldsFromMenuMap('categories'),
  },
  modifierGroups: {
    id: 'modifierGroups',
    menuKey: '/modifier-groups',
    href: '/modifier-groups',
    labelKey: 'nav.addonGroups',
    icon: 'GroupOutlined',
    permission: PERMISSIONS.PRODUCT_VIEW,
  },
  pricingRules: {
    id: 'pricingRules',
    menuKey: '/pricing-rules',
    href: '/pricing-rules',
    labelKey: 'nav.priceRules',
    icon: 'TagOutlined',
    permission: PERMISSIONS.PRODUCT_VIEW,
  },
  inventory: {
    id: 'inventory',
    menuKey: '/inventory',
    href: '/inventory',
    labelKey: 'nav.inventory',
    icon: 'InboxOutlined',
    permission: PERMISSIONS.INVENTORY_VIEW,
  },
  customers: {
    id: 'customers',
    menuKey: '/customers',
    href: '/customers',
    labelKey: 'nav.customerList',
    icon: 'UserOutlined',
    ...sidebarFieldsFromMenuMap('customers'),
  },
  benefitDefinitions: {
    id: 'benefitDefinitions',
    menuKey: '/benefit-definitions',
    href: '/benefit-definitions',
    labelKey: 'nav.benefitDefinitions',
    icon: 'GiftOutlined',
    sidebarHidden: true,
    ...sidebarFieldsFromMenuMap('benefits'),
  },
  benefitAssignments: {
    id: 'benefitAssignments',
    menuKey: '/benefit-assignments',
    href: '/benefit-assignments',
    labelKey: 'nav.benefitAssignments',
    icon: 'TagOutlined',
    sidebarHidden: true,
  },
  reportingDashboard: {
    id: 'reportingDashboard',
    menuKey: '/dashboard',
    href: '/dashboard',
    labelKey: 'nav.overview',
    icon: 'DashboardOutlined',
    permission: ANY_AUTHENTICATED_PERMISSION,
  },
  reportingOverview: {
    id: 'reportingOverview',
    menuKey: '/reporting',
    href: '/reporting',
    labelKey: 'nav.operationalReports',
    icon: 'PieChartOutlined',
    ...sidebarFieldsFromMenuMap('reports'),
  },
  reportCenter: {
    id: 'reportCenter',
    menuKey: '/reporting/report-center',
    href: '/reporting/report-center',
    labelKey: 'nav.reportCenter',
    icon: 'FundOutlined',
    permission: PERMISSIONS.REPORT_VIEW,
  },
  staffPerformance: {
    id: 'staffPerformance',
    menuKey: '/reporting/staff',
    href: '/reporting/staff',
    labelKey: 'nav.staffPerformance',
    icon: 'TeamOutlined',
    permission: PERMISSIONS.REPORT_VIEW,
  },
  activityLog: {
    id: 'activityLog',
    menuKey: '/audit-logs/activity',
    href: '/audit-logs/activity',
    labelKey: 'nav.activityLog',
    icon: 'UnorderedListOutlined',
    permission: PERMISSIONS.AUDIT_VIEW,
  },
  dailyClosingSummary: {
    id: 'dailyClosingSummary',
    menuKey: '/reports/daily-closing',
    href: '/reports/daily-closing',
    labelKey: 'nav.dailyClosingSummary',
    icon: 'SnippetsOutlined',
    sidebarHidden: true,
  },
  complianceReports: {
    id: 'complianceReports',
    menuKey: '/reporting/compliance',
    href: '/reporting/compliance',
    labelKey: 'nav.complianceReports',
    icon: 'SafetyCertificateOutlined',
    sidebarHidden: true,
  },
  userActivityReport: {
    id: 'userActivityReport',
    menuKey: '/admin/reports/user-activity',
    href: '/admin/reports/user-activity',
    labelKey: 'nav.userActivity',
    icon: 'TeamOutlined',
    permission: PERMISSIONS.REPORT_VIEW,
  },
  tagesbericht: {
    id: 'tagesbericht',
    menuKey: '/reporting/tagesbericht',
    href: '/reporting/tagesbericht',
    labelKey: 'nav.tagesbericht',
    icon: 'FileDoneOutlined',
    sidebarHidden: true,
    permission: PERMISSIONS.REPORT_VIEW,
  },
  monatsbericht: {
    id: 'monatsbericht',
    menuKey: '/reporting/monatsbericht',
    href: '/reporting/monatsbericht',
    labelKey: 'nav.monatsbericht',
    icon: 'BarChartOutlined',
    sidebarHidden: true,
  },
  jahresbericht: {
    id: 'jahresbericht',
    menuKey: '/reporting/jahresbericht',
    href: '/reporting/jahresbericht',
    labelKey: 'nav.jahresbericht',
    icon: 'AreaChartOutlined',
    sidebarHidden: true,
  },
  auditLogs: {
    id: 'auditLogs',
    menuKey: '/audit-logs',
    href: '/audit-logs',
    labelKey: 'nav.auditLogs',
    icon: 'SafetyCertificateOutlined',
    permission: PERMISSIONS.AUDIT_VIEW,
  },
  fiscalExportAuditLogs: {
    id: 'fiscalExportAuditLogs',
    menuKey: '/admin/audit/fiscal-exports',
    href: '/admin/audit/fiscal-exports',
    labelKey: 'nav.fiscalExportAuditLogs',
    icon: 'CloudDownloadOutlined',
    permission: PERMISSIONS.AUDIT_VIEW,
  },
  /** Legacy TSE offline intents (`offline_transactions`). New full-order snapshots: RKSV hub → Diagnose → `rksvAdminMenuModel` `/rksv/offline-orders` (`payment.view`). */
  offlineTransactionsAdmin: {
    id: 'offlineTransactionsAdmin',
    menuKey: '/admin/tse/offline-transactions',
    href: '/admin/tse/offline-transactions',
    labelKey: 'nav.offlineTransactionsAdmin',
    icon: 'InboxOutlined',
    permission: PERMISSIONS.PAYMENT_VIEW,
  },
  users: {
    id: 'users',
    menuKey: '/admin/users',
    href: '/admin/users',
    labelKey: 'nav.users',
    icon: 'TeamOutlined',
    ...sidebarFieldsFromMenuMap('users'),
  },
  accessOverview: {
    id: 'accessOverview',
    menuKey: '/admin/access',
    href: '/admin/access',
    labelKey: 'nav.overview',
    icon: 'KeyOutlined',
    permission: PERMISSIONS.USER_VIEW,
  },
  accessRoles: {
    id: 'accessRoles',
    menuKey: '/admin/access/roles',
    href: '/admin/access/roles',
    labelKey: 'nav.rolesPermissions',
    icon: 'SafetyOutlined',
    permission: PERMISSIONS.ROLE_MANAGE,
  },
  accessMatrix: {
    id: 'accessMatrix',
    menuKey: '/admin/access/matrix',
    href: '/admin/access/matrix',
    labelKey: 'nav.permissionsOverview',
    icon: 'AuditOutlined',
    permission: PERMISSIONS.ROLE_VIEW,
  },
  accessPermissionHistory: {
    id: 'accessPermissionHistory',
    menuKey: '/admin/access/permission-history',
    href: '/admin/access/permission-history',
    labelKey: 'nav.permissionHistory',
    icon: 'HistoryOutlined',
    permission: PERMISSIONS.AUDIT_VIEW,
  },
  accessPermissionRequests: {
    id: 'accessPermissionRequests',
    menuKey: '/admin/access/permission-requests',
    href: '/admin/access/permission-requests',
    labelKey: 'nav.permissionRequests',
    icon: 'AuditOutlined',
    permission: PERMISSIONS.SYSTEM_CRITICAL,
  },
  accessPermissionPackages: {
    id: 'accessPermissionPackages',
    menuKey: '/admin/access/permission-packages',
    href: '/admin/access/permission-packages',
    labelKey: 'nav.permissionPackages',
    icon: 'AppstoreOutlined',
    permission: PERMISSIONS.ROLE_VIEW,
  },
  accessPermissionBackups: {
    id: 'accessPermissionBackups',
    menuKey: '/admin/access/permission-backups',
    href: '/admin/access/permission-backups',
    labelKey: 'nav.permissionBackups',
    icon: 'ToolOutlined',
    permission: PERMISSIONS.SYSTEM_CRITICAL,
  },
  accessPermissionStats: {
    id: 'accessPermissionStats',
    menuKey: '/admin/access/permission-stats',
    href: '/admin/access/permission-stats',
    labelKey: 'nav.permissionStats',
    icon: 'PieChartOutlined',
    permission: PERMISSIONS.SYSTEM_CRITICAL,
  },
  companySettings: {
    id: 'companySettings',
    menuKey: '/settings/company',
    href: '/settings/company',
    labelKey: 'nav.companyFiscal',
    icon: 'ShopOutlined',
    permission: PERMISSIONS.SETTINGS_MANAGE,
  },
  workingHours: {
    id: 'workingHours',
    menuKey: '/settings/working-hours',
    href: '/settings/working-hours',
    labelKey: 'nav.workingHours',
    icon: 'CalendarOutlined',
    ...sidebarFieldsFromMenuMap('workingHours'),
  },
  websiteGenerator: {
    id: 'websiteGenerator',
    menuKey: '/settings/website',
    href: '/settings/website',
    labelKey: 'nav.websiteGenerator',
    icon: 'GlobalOutlined',
    permission: [
      PERMISSIONS.DIGITAL_VIEW,
      PERMISSIONS.DIGITAL_PREVIEW,
      PERMISSIONS.DIGITAL_REQUEST,
      PERMISSIONS.DIGITAL_CREATE,
      PERMISSIONS.WEBSITE_MANAGE,
    ],
  },
  digitalPortal: {
    id: 'digitalPortal',
    menuKey: '/settings/digital',
    href: '/settings/digital',
    labelKey: 'nav.digitalPortal',
    icon: 'GlobalOutlined',
    ...sidebarFieldsFromMenuMap('digitalServices'),
  },
  customerPortal: {
    id: 'customerPortal',
    menuKey: '/digital/customer-portal',
    href: '/digital/customer-portal',
    labelKey: 'nav.customerPortal',
    icon: 'GlobalOutlined',
    permission: [PERMISSIONS.DIGITAL_VIEW, PERMISSIONS.DIGITAL_REQUEST, PERMISSIONS.WEBSITE_MANAGE],
  },
  settingsHub: {
    id: 'settingsHub',
    menuKey: '/settings',
    href: '/settings',
    labelKey: 'nav.settingsHub',
    icon: 'SettingOutlined',
    ...sidebarFieldsFromMenuMap('settings'),
  },
  tseSettings: {
    id: 'tseSettings',
    menuKey: '/settings/tse',
    href: '/settings/tse',
    labelKey: 'settings.tabs.tse',
    icon: 'SafetyCertificateOutlined',
    permission: PERMISSIONS.SETTINGS_MANAGE,
    sidebarHidden: true,
  },
  finanzonlineSettings: {
    id: 'finanzonlineSettings',
    menuKey: '/settings/finanzonline',
    href: '/settings/finanzonline',
    labelKey: 'settings.tabs.finanzOnline',
    icon: 'WalletOutlined',
    permission: PERMISSIONS.SETTINGS_MANAGE,
    sidebarHidden: true,
  },
  backupSettings: {
    id: 'backupSettings',
    menuKey: '/settings/backup',
    href: '/settings/backup',
    labelKey: 'settings.manager.advanced.backup',
    icon: 'CloudServerOutlined',
    permission: PERMISSIONS.BACKUP_MANAGE,
    sidebarHidden: true,
  },
  settingsDataManagement: {
    id: 'settingsDataManagement',
    menuKey: '/settings/data-management',
    href: '/settings/data-management',
    labelKey: 'nav.dataManagement',
    icon: 'DatabaseOutlined',
    permission: PERMISSIONS.BACKUP_MANAGE,
  },
  sessionSettings: {
    id: 'sessionSettings',
    menuKey: '/settings/session',
    href: '/settings/session',
    labelKey: 'nav.sessionInactivity',
    icon: 'ClockCircleOutlined',
    permission: PERMISSIONS.SETTINGS_VIEW,
  },
  activeSessions: {
    id: 'activeSessions',
    menuKey: '/settings/sessions',
    href: '/settings/sessions',
    labelKey: 'nav.activeSessions',
    icon: 'LaptopOutlined',
    permission: PERMISSIONS.SETTINGS_VIEW,
  },
  offlineSettings: {
    id: 'offlineSettings',
    menuKey: '/settings/offline',
    href: '/settings/offline',
    labelKey: 'nav.offlineSettings',
    icon: 'DisconnectOutlined',
    permission: PERMISSIONS.SETTINGS_MANAGE,
    sidebarHidden: true,
  },
  personalization: {
    id: 'personalization',
    menuKey: '/settings/personalization',
    href: '/settings/personalization',
    labelKey: 'nav.appearanceLanguage',
    icon: 'BgColorsOutlined',
    permission: PERMISSIONS.SETTINGS_VIEW,
  },
  preferences: {
    id: 'preferences',
    menuKey: '/settings/preferences',
    href: '/settings/preferences',
    labelKey: 'nav.dateTimePreferences',
    icon: 'ClockCircleOutlined',
    permission: PERMISSIONS.SETTINGS_VIEW,
  },
  paymentMethods: {
    id: 'paymentMethods',
    menuKey: '/settings/payment-methods',
    href: '/settings/payment-methods',
    labelKey: 'nav.paymentMethods',
    icon: 'CreditCardOutlined',
    permission: PERMISSIONS.SETTINGS_VIEW,
  },
  paymentGateway: {
    id: 'paymentGateway',
    menuKey: '/settings/payment',
    href: '/settings/payment',
    labelKey: 'nav.paymentGateway',
    icon: 'CreditCardOutlined',
    permission: PERMISSIONS.SETTINGS_MANAGE,
  },
  backupDr: {
    id: 'backupDr',
    menuKey: '/backup',
    href: '/backup',
    labelKey: 'nav.backupOverview',
    icon: 'DashboardOutlined',
    ...sidebarFieldsFromMenuMap('backup'),
  },
  backupRuns: {
    id: 'backupRuns',
    menuKey: '/backup/runs',
    href: '/backup/runs',
    labelKey: 'nav.backupRuns',
    icon: 'DatabaseOutlined',
    permission: PERMISSIONS.SETTINGS_VIEW,
  },
  backupSchedule: {
    id: 'backupSchedule',
    menuKey: '/backup/configuration/schedule',
    href: '/backup/configuration#backup-dr-schedule-settings',
    labelKey: 'nav.backupScheduleRetention',
    icon: 'ClockCircleOutlined',
    permission: PERMISSIONS.BACKUP_MANAGE,
  },
  backupPlatform: {
    id: 'backupPlatform',
    menuKey: '/backup/configuration/platform',
    href: '/backup/configuration',
    labelKey: 'nav.backupPlatformSettings',
    icon: 'CloudSyncOutlined',
    permission: PERMISSIONS.SETTINGS_MANAGE,
  },
  backupAuditLog: {
    id: 'backupAuditLog',
    menuKey: '/backup/audit',
    href: '/backup/audit',
    labelKey: 'nav.backupAuditLog',
    icon: 'AuditOutlined',
    permission: PERMISSIONS.SETTINGS_VIEW,
  },
  developmentMode: {
    id: 'developmentMode',
    menuKey: '/settings/development-mode',
    href: '/settings/development-mode',
    labelKey: 'nav.developmentMode',
    icon: 'ExperimentOutlined',
    sidebarHidden: true,
  },
  timeSync: {
    id: 'timeSync',
    menuKey: '/admin/system/time-sync',
    href: '/admin/system/time-sync',
    labelKey: 'nav.timeSync',
    icon: 'ClockCircleOutlined',
    sidebarHidden: true,
  },
  licenseManagement: {
    id: 'licenseManagement',
    menuKey: '/admin/license',
    href: '/admin/license',
    labelKey: 'nav.licenses',
    icon: 'KeyOutlined',
    /** Mandant license (Manager) + deployment license (Super Admin / settings.manage). */
    permission: [PERMISSIONS.LICENSE_MANAGE, PERMISSIONS.SETTINGS_MANAGE],
  },
  /** Dev-only — hidden outside `NODE_ENV=development` via `developmentOnly` + `buildAdminSidebar`. */
  licenseTest: {
    id: 'licenseTest',
    menuKey: '/admin/license/test',
    href: '/admin/license/test',
    labelKey: 'nav.licenseTest',
    icon: 'ExperimentOutlined',
    permission: PERMISSIONS.SYSTEM_CRITICAL,
    developmentOnly: true,
  },
  licenseDebug: {
    id: 'licenseDebug',
    menuKey: '/admin/license/debug',
    href: '/admin/license/debug',
    labelKey: 'nav.licenseDebug',
    icon: 'BugOutlined',
    permission: PERMISSIONS.SYSTEM_CRITICAL,
  },
  superAdminTenants: {
    id: 'superAdminTenants',
    menuKey: '/admin/tenants',
    href: '/admin/tenants',
    labelKey: 'nav.tenants',
    icon: 'ApartmentOutlined',
    ...sidebarFieldsFromMenuMap('tenants'),
  },
  superAdminApprovals: {
    id: 'superAdminApprovals',
    menuKey: '/admin/approvals',
    href: '/admin/approvals',
    labelKey: 'nav.approvals',
    icon: 'AuditOutlined',
    permission: PERMISSIONS.SYSTEM_CRITICAL,
  },
  superAdminMaintenance: {
    id: 'superAdminMaintenance',
    menuKey: '/admin/maintenance',
    href: '/admin/maintenance',
    labelKey: 'nav.maintenance',
    icon: 'ToolOutlined',
    permission: PERMISSIONS.SYSTEM_CRITICAL,
  },
  superAdminDataManagement: {
    id: 'superAdminDataManagement',
    menuKey: '/admin/data-management',
    href: '/admin/data-management',
    labelKey: 'nav.dataManagement',
    icon: 'DatabaseOutlined',
    permission: PERMISSIONS.SYSTEM_CRITICAL,
  },
  elmahErrors: {
    id: 'elmahErrors',
    menuKey: '/admin/errors',
    href: '/admin/errors',
    labelKey: 'nav.errorLogs',
    icon: 'FileSearchOutlined',
    permission: PERMISSIONS.SYSTEM_CRITICAL,
  },
  superAdminLicenses: {
    id: 'superAdminLicenses',
    menuKey: '/admin/licenses',
    href: '/admin/licenses',
    labelKey: 'nav.platformLicenses',
    icon: 'SafetyOutlined',
    ...sidebarFieldsFromMenuMap('license'),
  },
  superAdminCashRegisters: {
    id: 'superAdminCashRegisters',
    menuKey: '/admin/cash-registers',
    href: '/admin/cash-registers',
    labelKey: 'nav.superAdminCashRegisters',
    icon: 'ShopOutlined',
    permission: PERMISSIONS.SYSTEM_CRITICAL,
    sidebarHidden: true,
  },
  billingOverview: {
    id: 'billingOverview',
    menuKey: '/admin/billing',
    href: '/admin/billing',
    labelKey: 'nav.licenseSales',
    icon: 'CreditCardOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  billingSales: {
    id: 'billingSales',
    menuKey: '/admin/billing/sales',
    href: '/admin/billing/sales',
    labelKey: 'nav.billingSales',
    icon: 'FileTextOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
    sidebarHidden: true,
  },
  billingSalesNew: {
    id: 'billingSalesNew',
    menuKey: '/admin/billing/sales/new',
    href: '/admin/billing/sales/new',
    labelKey: 'nav.billingSalesNew',
    icon: 'ShoppingOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
    sidebarHidden: true,
  },
  billingStats: {
    id: 'billingStats',
    menuKey: '/admin/billing/stats',
    href: '/admin/billing/stats',
    labelKey: 'nav.billingStats',
    icon: 'BarChartOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
    sidebarHidden: true,
  },
  billingDigital: {
    id: 'billingDigital',
    menuKey: '/billing/digital',
    href: '/billing/digital',
    labelKey: 'nav.billingDigital',
    icon: 'FundOutlined',
    permission: [PERMISSIONS.DIGITAL_MANAGE, PERMISSIONS.SYSTEM_CRITICAL],
  },
  digitalServicesManage: {
    id: 'digitalServicesManage',
    menuKey: '/admin/digital',
    href: '/admin/digital',
    labelKey: 'nav.digitalServicesManage',
    icon: 'GlobalOutlined',
    permission: [
      PERMISSIONS.DIGITAL_MANAGE,
      PERMISSIONS.DIGITAL_ACTIVATE,
      PERMISSIONS.DIGITAL_PRICING_MANAGE,
      PERMISSIONS.SYSTEM_CRITICAL,
    ],
  },
  digitalServiceRequests: {
    id: 'digitalServiceRequests',
    menuKey: '/admin/digital/requests',
    href: '/admin/digital/requests',
    labelKey: 'nav.digitalServiceRequests',
    icon: 'FileSearchOutlined',
    permission: [PERMISSIONS.DIGITAL_MANAGE, PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminFeedbackInbox: {
    id: 'adminFeedbackInbox',
    menuKey: '/admin/feedback',
    href: '/admin/feedback',
    labelKey: 'nav.adminFeedback',
    icon: 'InboxOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminMonitoring: {
    id: 'adminMonitoring',
    menuKey: '/admin/monitoring',
    href: '/admin/monitoring',
    labelKey: 'nav.adminMonitoring',
    icon: 'DashboardOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminRiskDashboard: {
    id: 'adminRiskDashboard',
    menuKey: '/admin/risk-dashboard',
    href: '/admin/risk-dashboard',
    labelKey: 'nav.adminRiskDashboard',
    icon: 'AuditOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseManagement: {
    id: 'adminTseManagement',
    menuKey: '/admin/tse-management',
    href: '/admin/tse-management',
    labelKey: 'nav.adminTseManagement',
    icon: 'SafetyCertificateOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseFailover: {
    id: 'adminTseFailover',
    menuKey: '/admin/tse/failover',
    href: '/admin/tse/failover',
    labelKey: 'nav.adminTseFailover',
    icon: 'SwapOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseResourcePools: {
    id: 'adminTseResourcePools',
    menuKey: '/admin/tse/resource-pools',
    href: '/admin/tse/resource-pools',
    labelKey: 'nav.adminTseResourcePools',
    icon: 'ClusterOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseIncidents: {
    id: 'adminTseIncidents',
    menuKey: '/admin/tse/incidents',
    href: '/admin/tse/incidents',
    labelKey: 'nav.adminTseIncidents',
    icon: 'AlertOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseSla: {
    id: 'adminTseSla',
    menuKey: '/admin/tse/sla',
    href: '/admin/tse/sla',
    labelKey: 'nav.adminTseSla',
    icon: 'FundOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseCapacity: {
    id: 'adminTseCapacity',
    menuKey: '/admin/tse/capacity',
    href: '/admin/tse/capacity',
    labelKey: 'nav.adminTseCapacity',
    icon: 'BarChartOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseCost: {
    id: 'adminTseCost',
    menuKey: '/admin/tse/cost',
    href: '/admin/tse/cost',
    labelKey: 'nav.adminTseCost',
    icon: 'WalletOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseDr: {
    id: 'adminTseDr',
    menuKey: '/admin/tse/disaster-recovery',
    href: '/admin/tse/disaster-recovery',
    labelKey: 'nav.adminTseDr',
    icon: 'CloudSyncOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseLogs: {
    id: 'adminTseLogs',
    menuKey: '/admin/tse/logs',
    href: '/admin/tse/logs',
    labelKey: 'nav.adminTseLogs',
    icon: 'FileSearchOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseDeveloperTools: {
    id: 'adminTseDeveloperTools',
    menuKey: '/admin/tse/developer-tools',
    href: '/admin/tse/developer-tools',
    labelKey: 'nav.adminTseDeveloperTools',
    icon: 'ToolOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseCompliance: {
    id: 'adminTseCompliance',
    menuKey: '/admin/tse/compliance',
    href: '/admin/tse/compliance',
    labelKey: 'nav.adminTseCompliance',
    icon: 'SafetyCertificateOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseAutoScaling: {
    id: 'adminTseAutoScaling',
    menuKey: '/admin/tse/auto-scaling',
    href: '/admin/tse/auto-scaling',
    labelKey: 'nav.adminTseAutoScaling',
    icon: 'ClusterOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseAutoHealing: {
    id: 'adminTseAutoHealing',
    menuKey: '/admin/tse/auto-healing',
    href: '/admin/tse/auto-healing',
    labelKey: 'nav.adminTseAutoHealing',
    icon: 'DisconnectOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseKnowledge: {
    id: 'adminTseKnowledge',
    menuKey: '/admin/tse/knowledge',
    href: '/admin/tse/knowledge',
    labelKey: 'nav.adminTseKnowledge',
    icon: 'FileSearchOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseApiGateway: {
    id: 'adminTseApiGateway',
    menuKey: '/admin/tse/api-gateway',
    href: '/admin/tse/api-gateway',
    labelKey: 'nav.adminTseApiGateway',
    icon: 'SwapOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseAnomalies: {
    id: 'adminTseAnomalies',
    menuKey: '/admin/tse/anomalies',
    href: '/admin/tse/anomalies',
    labelKey: 'nav.adminTseAnomalies',
    icon: 'AlertOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseAnalytics: {
    id: 'adminTseAnalytics',
    menuKey: '/admin/tse/analytics',
    href: '/admin/tse/analytics',
    labelKey: 'nav.adminTseAnalytics',
    icon: 'BarChartOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseUserAnalytics: {
    id: 'adminTseUserAnalytics',
    menuKey: '/admin/tse/user-analytics',
    href: '/admin/tse/user-analytics',
    labelKey: 'nav.adminTseUserAnalytics',
    icon: 'TeamOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseRecommendations: {
    id: 'adminTseRecommendations',
    menuKey: '/admin/tse/recommendations',
    href: '/admin/tse/recommendations',
    labelKey: 'nav.adminTseRecommendations',
    icon: 'ThunderboltOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseUpdates: {
    id: 'adminTseUpdates',
    menuKey: '/admin/tse/updates',
    href: '/admin/tse/updates',
    labelKey: 'nav.adminTseUpdates',
    icon: 'CloudSyncOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseSustainability: {
    id: 'adminTseSustainability',
    menuKey: '/admin/tse/sustainability',
    href: '/admin/tse/sustainability',
    labelKey: 'nav.adminTseSustainability',
    icon: 'ExperimentOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseWebhooks: {
    id: 'adminTseWebhooks',
    menuKey: '/admin/tse/webhooks',
    href: '/admin/tse/webhooks',
    labelKey: 'nav.adminTseWebhooks',
    icon: 'ApiOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseBlockchain: {
    id: 'adminTseBlockchain',
    menuKey: '/admin/tse/blockchain',
    href: '/admin/tse/blockchain',
    labelKey: 'nav.adminTseBlockchain',
    icon: 'ClusterOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  adminTseTraining: {
    id: 'adminTseTraining',
    menuKey: '/admin/tse/training',
    href: '/admin/tse/training',
    labelKey: 'nav.adminTseTraining',
    icon: 'ExperimentOutlined',
    permission: [PERMISSIONS.SYSTEM_CRITICAL],
  },
  rksvTestsDepExport: {
    id: 'rksvTestsDepExport',
    menuKey: '/admin/rksv/dep-export',
    href: '/admin/rksv/dep-export',
    labelKey: 'nav.depExport',
    icon: 'SafetyCertificateOutlined',
    permission: [PERMISSIONS.REPORT_EXPORT, PERMISSIONS.AUDIT_VIEW],
  },
  rksvTestsSignatureVerify: {
    id: 'rksvTestsSignatureVerify',
    menuKey: '/admin/rksv/signature-verify',
    href: '/admin/rksv/signature-verify',
    labelKey: 'nav.signatureVerification',
    icon: 'SafetyCertificateOutlined',
    permission: PERMISSIONS.AUDIT_VIEW,
  },
  rksvStatusOverview: {
    id: 'rksvStatusOverview',
    menuKey: '/rksv/operations',
    href: '/rksv',
    labelKey: 'nav.rksvStatus',
    icon: 'SafetyOutlined',
    ...sidebarFieldsFromMenuMap('rksv'),
  },
  rksvSystemStatus: {
    id: 'rksvSystemStatus',
    menuKey: '/rksv/status',
    href: '/rksv/status',
    labelKey: 'nav.systemStatus',
    icon: 'ControlOutlined',
    permission: PERMISSIONS.SETTINGS_VIEW,
  },
  rksvFinanzOnline: {
    id: 'rksvFinanzOnline',
    menuKey: '/rksv/finanz-online-outbox',
    href: '/rksv/finanz-online-outbox',
    labelKey: 'nav.finanzOnline',
    icon: 'CloudSyncOutlined',
    permission: PERMISSIONS.FINANZONLINE_VIEW,
  },
  rksvOfflineOrders: {
    id: 'rksvOfflineOrders',
    menuKey: '/rksv/offline-orders',
    href: '/rksv/offline-orders',
    labelKey: 'nav.offlineOrders',
    icon: 'InboxOutlined',
    permission: PERMISSIONS.PAYMENT_VIEW,
  },
  specialReceiptStart: {
    id: 'specialReceiptStart',
    menuKey: '/rksv/sb/startbeleg',
    href: '/rksv/sonderbelege?focus=startbeleg',
    labelKey: 'nav.startReceipt',
    icon: 'FileDoneOutlined',
    permission: PERMISSIONS.FINANZONLINE_MANAGE,
  },
  specialReceiptMonthly: {
    id: 'specialReceiptMonthly',
    menuKey: '/rksv/sb/monatsbeleg',
    href: '/rksv/sonderbelege?focus=monatsbeleg',
    labelKey: 'nav.monthlyReceipt',
    icon: 'FileDoneOutlined',
    permission: PERMISSIONS.FINANZONLINE_MANAGE,
  },
  specialReceiptYearly: {
    id: 'specialReceiptYearly',
    menuKey: '/rksv/sb/jahresbeleg',
    href: '/rksv/sonderbelege?focus=jahresbeleg',
    labelKey: 'nav.yearlyReceipt',
    icon: 'FileDoneOutlined',
    permission: PERMISSIONS.FINANZONLINE_MANAGE,
  },
  specialReceiptNull: {
    id: 'specialReceiptNull',
    menuKey: '/rksv/sb/nullbeleg',
    href: '/rksv/sonderbelege?focus=nullbeleg',
    labelKey: 'nav.nullReceipt',
    icon: 'FileDoneOutlined',
    permission: PERMISSIONS.FINANZONLINE_MANAGE,
  },
  specialReceiptClosing: {
    id: 'specialReceiptClosing',
    menuKey: '/rksv/sb/schlussbeleg',
    href: '/rksv/sonderbelege?focus=schlussbeleg',
    labelKey: 'nav.closingReceipt',
    icon: 'FileDoneOutlined',
    permission: PERMISSIONS.FINANZONLINE_MANAGE,
  },
  specialReceiptTestHelper: {
    id: 'specialReceiptTestHelper',
    menuKey: '/rksv/sb/test-helper',
    href: '/rksv/sonderbelege?focus=test-helper',
    labelKey: 'nav.rksvTestHelper',
    icon: 'ExperimentOutlined',
    permission: PERMISSIONS.SYSTEM_CRITICAL,
  },
};

export const SIDEBAR_GROUP_META: Record<
  SidebarGroupId,
  { menuKey: string; labelKey: string; icon: SidebarIconToken }
> = {
  dashboard: {
    menuKey: ADMIN_SIDEBAR_GROUP_KEYS.dashboard,
    labelKey: 'nav.dashboard',
    icon: 'DashboardOutlined',
  },
  license: {
    menuKey: ADMIN_SIDEBAR_GROUP_KEYS.license,
    labelKey: 'nav.licenseManagement',
    icon: 'CreditCardOutlined',
  },
  operations: {
    menuKey: ADMIN_SIDEBAR_GROUP_KEYS.operations,
    labelKey: 'nav.operations',
    icon: 'ThunderboltOutlined',
  },
  rksv: {
    menuKey: ADMIN_SIDEBAR_GROUP_KEYS.rksv,
    labelKey: 'nav.rksv',
    icon: 'SafetyOutlined',
  },
  catalog: {
    menuKey: ADMIN_SIDEBAR_GROUP_KEYS.catalog,
    labelKey: 'nav.catalog',
    icon: 'AppstoreOutlined',
  },
  customers: {
    menuKey: ADMIN_SIDEBAR_GROUP_KEYS.customers,
    labelKey: 'nav.customers',
    icon: 'UsergroupAddOutlined',
  },
  reports: {
    menuKey: ADMIN_SIDEBAR_GROUP_KEYS.reports,
    labelKey: 'nav.reports',
    icon: 'LineChartOutlined',
  },
  backup: {
    menuKey: ADMIN_SIDEBAR_GROUP_KEYS.backup,
    labelKey: 'nav.backupDisasterRecovery',
    icon: 'CloudServerOutlined',
  },
  settings: {
    menuKey: ADMIN_SIDEBAR_GROUP_KEYS.settings,
    labelKey: 'nav.settings',
    icon: 'SettingOutlined',
  },
  administration: {
    menuKey: ADMIN_SIDEBAR_GROUP_KEYS.admin,
    labelKey: 'nav.administration',
    icon: 'ToolOutlined',
  },
};

/** @deprecated Use `SIDEBAR_GROUP_META` */
export const SIDEBAR_DOMAIN_GROUP_META = SIDEBAR_GROUP_META;

export type SidebarCatalogId = keyof typeof SIDEBAR_NAV_ITEM_CATALOG;

export type SidebarLayoutBlock =
  | { kind: 'leaves'; catalogIds: SidebarCatalogId[] }
  | {
      kind: 'nested';
      menuKey: string;
      labelKey: string;
      icon: SidebarIconToken;
      catalogIds: SidebarCatalogId[];
    }
  | { kind: 'fiscalRksvClosing'; menuKey: string; labelKey: string; icon: SidebarIconToken }
  | { kind: 'rksvHub'; menuKey: '/rksv'; labelKey: string; icon: SidebarIconToken };

export type SidebarLayoutRow =
  | { kind: 'divider'; key: string }
  | { kind: 'leaves'; catalogIds: SidebarCatalogId[] }
  | {
      kind: 'nested';
      menuKey: string;
      labelKey: string;
      icon: SidebarIconToken;
      catalogIds: SidebarCatalogId[];
    }
  | { kind: 'group'; group: SidebarGroupId; blocks: SidebarLayoutBlock[] };

/**
 * Top-to-bottom shell layout (order = render order).
 * Nine primary groups: Dashboard → Lizenzverwaltung → Betrieb → RKSV → Sortiment → Kunden → Berichte → Einstellungen → Verwaltung.
 */
export const SIDEBAR_LAYOUT_ROWS: SidebarLayoutRow[] = [
  {
    kind: 'group',
    group: 'dashboard',
    blocks: [{ kind: 'leaves', catalogIds: ['reportingDashboard'] }],
  },
  {
    kind: 'group',
    group: 'license',
    blocks: [
      {
        kind: 'leaves',
        catalogIds: [
          'licenseManagement',
          'licenseTest',
          'superAdminLicenses',
          'billingOverview',
          'billingDigital',
        ],
      },
      {
        kind: 'nested',
        menuKey: ADMIN_SIDEBAR_GROUP_KEYS.digitalAdmin,
        labelKey: 'nav.digital',
        icon: 'GlobalOutlined',
        catalogIds: ['digitalServicesManage', 'digitalServiceRequests', 'adminFeedbackInbox'],
      },
    ],
  },
  {
    kind: 'group',
    group: 'operations',
    blocks: [
      {
        kind: 'leaves',
        catalogIds: [
          'operationsCenter',
          'tables',
          'kassenverwaltung',
          'staffHub',
          'shiftsOverview',
        ],
      },
      {
        kind: 'nested',
        menuKey: ADMIN_SIDEBAR_GROUP_KEYS.salesTransactions,
        labelKey: 'nav.sales',
        icon: 'ShoppingCartOutlined',
        catalogIds: [
          'receipts',
          'onlineOrders',
          'payments',
          'paymentTrends',
          'cardTransactions',
          'stornoRefundAudit',
          'vouchers',
          'invoices',
        ],
      },
      { kind: 'leaves', catalogIds: ['tagesabschluss'] },
    ],
  },
  {
    kind: 'group',
    group: 'rksv',
    blocks: [
      { kind: 'leaves', catalogIds: ['rksvStatusOverview'] },
      {
        kind: 'nested',
        menuKey: ADMIN_SIDEBAR_GROUP_KEYS.specialReceipts,
        labelKey: 'nav.specialReceipts',
        icon: 'SafetyCertificateOutlined',
        catalogIds: [
          'specialReceiptStart',
          'specialReceiptMonthly',
          'specialReceiptYearly',
          'specialReceiptNull',
          'specialReceiptClosing',
          'specialReceiptTestHelper',
        ],
      },
      {
        kind: 'leaves',
        catalogIds: [
          'rksvFinanzOnline',
          'rksvOfflineOrders',
          'rksvTestsDepExport',
          'rksvTestsSignatureVerify',
          'auditLogs',
          'rksvSystemStatus',
          'fiscalExportAuditLogs',
          'offlineTransactionsAdmin',
        ],
      },
      {
        kind: 'rksvHub',
        menuKey: '/rksv',
        labelKey: 'nav.rksvAdvancedHub',
        icon: 'SafetyOutlined',
      },
    ],
  },
  {
    kind: 'group',
    group: 'catalog',
    blocks: [
      {
        kind: 'leaves',
        catalogIds: ['products', 'categories', 'modifierGroups', 'pricingRules', 'inventory'],
      },
    ],
  },
  {
    kind: 'group',
    group: 'customers',
    blocks: [{ kind: 'leaves', catalogIds: ['customers'] }],
  },
  {
    kind: 'group',
    group: 'reports',
    blocks: [
      {
        kind: 'leaves',
        catalogIds: [
          'reportingOverview',
          'reportCenter',
          'activityLog',
          'userActivityReport',
          'staffPerformance',
        ],
      },
    ],
  },
  {
    kind: 'group',
    group: 'backup',
    blocks: [
      { kind: 'leaves', catalogIds: ['backupDr', 'backupRuns'] },
      {
        kind: 'nested',
        menuKey: ADMIN_SIDEBAR_GROUP_KEYS.backupConfig,
        labelKey: 'nav.backupConfiguration',
        icon: 'SettingOutlined',
        catalogIds: ['backupSchedule', 'backupPlatform'],
      },
      { kind: 'leaves', catalogIds: ['backupAuditLog'] },
    ],
  },
  {
    kind: 'group',
    group: 'settings',
    blocks: [
      {
        kind: 'leaves',
        catalogIds: ['settingsHub', 'companySettings', 'workingHours', 'settingsDataManagement'],
      },
      {
        kind: 'nested',
        menuKey: ADMIN_SIDEBAR_GROUP_KEYS.digitalServices,
        labelKey: 'nav.digital',
        icon: 'GlobalOutlined',
        catalogIds: ['websiteGenerator', 'digitalPortal', 'customerPortal'],
      },
      {
        kind: 'leaves',
        catalogIds: [
          'tseSettings',
          'finanzonlineSettings',
          'sessionSettings',
          'activeSessions',
          'personalization',
          'preferences',
          'paymentMethods',
          'paymentGateway',
        ],
      },
    ],
  },
  {
    kind: 'group',
    group: 'administration',
    blocks: [
      {
        kind: 'nested',
        menuKey: ADMIN_SIDEBAR_GROUP_KEYS.accessArea,
        labelKey: 'nav.accessRoles',
        icon: 'KeyOutlined',
        catalogIds: [
          'accessOverview',
          'users',
          'accessRoles',
          'accessMatrix',
          'accessPermissionHistory',
          'accessPermissionRequests',
          'accessPermissionPackages',
          'accessPermissionBackups',
          'accessPermissionStats',
        ],
      },
      {
        kind: 'leaves',
        catalogIds: [
          'superAdminTenants',
          'superAdminApprovals',
          'superAdminMaintenance',
          'superAdminDataManagement',
          'superAdminCashRegisters',
          'elmahErrors',
          'adminMonitoring',
          'adminRiskDashboard',
          'adminTseManagement',
          'adminTseFailover',
          'adminTseResourcePools',
          'adminTseIncidents',
          'adminTseSla',
          'adminTseCapacity',
          'adminTseCost',
          'adminTseDr',
          'adminTseLogs',
          'adminTseDeveloperTools',
          'adminTseCompliance',
          'adminTseAutoScaling',
          'adminTseAutoHealing',
          'adminTseKnowledge',
          'adminTseApiGateway',
          'adminTseAnomalies',
          'adminTseAnalytics',
          'adminTseUserAnalytics',
          'adminTseRecommendations',
          'adminTseUpdates',
          'adminTseSustainability',
          'adminTseWebhooks',
          'adminTseBlockchain',
          'adminTseTraining',
        ],
      },
    ],
  },
];

export type AdminSidebarComposedData = RksvSidebarRegistryAttachment;

/**
 * Composes RKSV groups via plugin; used by `buildAdminSidebarMenuItems`.
 */
export function composeAdminSidebarData(
  t: (key: string) => string,
  verificationNavLabel: string
): AdminSidebarComposedData {
  const data: AdminSidebarComposedData = { rksvMenuGroups: [] };
  registerRksvSidebar(data, t, verificationNavLabel);
  return data;
}

/** Catalog leaf menuKeys visible in the sidebar (excludes `sidebarHidden`). */
export function getSidebarCatalogLeafMenuKeys(): string[] {
  return Object.values(SIDEBAR_NAV_ITEM_CATALOG)
    .filter((item) => !item.sidebarHidden)
    .map((item) => item.menuKey);
}

/** IA menu-area keys wired onto catalog leaves via `menuArea`. */
export function getWiredSidebarMenuAreas(): MenuPermissionMapKey[] {
  const areas = new Set<MenuPermissionMapKey>();
  for (const item of Object.values(SIDEBAR_NAV_ITEM_CATALOG)) {
    if (item.menuArea) areas.add(item.menuArea);
  }
  return [...areas];
}

/**
 * Validates that every catalog `menuArea` exists in `MENU_PERMISSION_MAP`.
 * Also logs unwired map keys via {@link logSidebarMenuPermissionMapWarnings}.
 */
export function validateSidebarMenuPermissionMappings(): string[] {
  const wired = getWiredSidebarMenuAreas();
  return validateMenuPermissions(wired);
}

/** Dev-only: warn when catalog menuAreas lack map entries or map keys are unwired. */
export function logSidebarMenuPermissionMapWarnings(): void {
  if (process.env.NODE_ENV !== 'development') return;
  logMenuPermissionMapWarnings(getWiredSidebarMenuAreas());
}

/** Catalog + RKSV leaves for guard coverage tests. */
export function getAllSidebarLeafMenuKeysForCoverage(rksvGroups: RksvMenuGroup[]): string[] {
  return [
    ...getSidebarCatalogLeafMenuKeys(),
    ...FISCAL_RKSV_CLOSING_VIRTUAL_MENU_KEYS,
    ...collectRksvMenuLeafKeys(rksvGroups),
  ];
}
