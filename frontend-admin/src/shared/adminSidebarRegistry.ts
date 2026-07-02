/**
 * Single source of truth for admin sidebar IA (non-RKSV leaves + shell layout).
 * RKSV subtree is attached via `registerRksvSidebar` in `composeAdminSidebarData`.
 *
 * Permission keys for menu filtering match `MENU_PERMISSION` map keys (= `menuKey` for leaves).
 */

import type { RksvMenuGroup } from '@/shared/rksvMenuModel';
import { collectRksvMenuLeafKeys } from '@/shared/rksvMenuModel';
import { FISCAL_RKSV_CLOSING_VIRTUAL_MENU_KEYS } from '@/shared/fiscalRksvClosingSidebar';
import { ADMIN_SIDEBAR_GROUP_KEYS } from '@/shared/adminSidebarNavigation';
import { registerRksvSidebar, type RksvSidebarRegistryAttachment } from '@/features/rksv/sidebarPlugin';
import { AppPermissions, PERMISSIONS, ANY_AUTHENTICATED_PERMISSION } from '@/shared/auth/permissions';

export type SidebarGroupId =
    | 'dashboard'
    | 'license'
    | 'operations'
    | 'rksv'
    | 'catalog'
    | 'customers'
    | 'reports'
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
    | 'SettingOutlined'
    | 'ShopOutlined'
    | 'BgColorsOutlined'
    | 'CloudServerOutlined'
    | 'ExperimentOutlined'
    | 'WalletOutlined'
    | 'ClockCircleOutlined'
    | 'CloudDownloadOutlined'
    | 'CloudSyncOutlined'
    | 'KeyOutlined'
    | 'ApartmentOutlined'
    | 'DisconnectOutlined';

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
    /** When true, route is registered but omitted from sidebar leaves (deep-link only). */
    sidebarHidden?: boolean;
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
    },
    tables: {
        id: 'tables',
        menuKey: '/tables',
        href: '/tables',
        labelKey: 'nav.tables',
        icon: 'TableOutlined',
    },
    kassenverwaltung: {
        id: 'kassenverwaltung',
        menuKey: '/kassenverwaltung',
        href: '/kassenverwaltung',
        labelKey: 'nav.cashRegisters',
        icon: 'ShopOutlined',
        permission: AppPermissions.CashRegisterManage,
    },
    shiftsOverview: {
        id: 'shiftsOverview',
        menuKey: '/shifts',
        href: '/shifts',
        labelKey: 'nav.shifts',
        icon: 'ClockCircleOutlined',
        permission: PERMISSIONS.SHIFT_VIEW,
    },
    tagesabschluss: {
        id: 'tagesabschluss',
        menuKey: '/tagesabschluss',
        href: '/tagesabschluss',
        labelKey: 'nav.dailyClosing',
        icon: 'CalendarOutlined',
    },
    receipts: {
        id: 'receipts',
        menuKey: '/receipts',
        href: '/receipts',
        labelKey: 'nav.receipts',
        icon: 'FileSearchOutlined',
    },
    payments: {
        id: 'payments',
        menuKey: '/payments',
        href: '/payments',
        labelKey: 'nav.payments',
        icon: 'CreditCardOutlined',
    },
    stornoRefundAudit: {
        id: 'stornoRefundAudit',
        menuKey: '/payments/storno-refund-audit',
        href: '/payments/storno-refund-audit',
        labelKey: 'nav.cancellations',
        icon: 'AuditOutlined',
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
    },
    invoices: {
        id: 'invoices',
        menuKey: '/invoices',
        href: '/invoices',
        labelKey: 'nav.invoices',
        icon: 'FileDoneOutlined',
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
        permission: PERMISSIONS.PRODUCT_VIEW,
    },
    categories: {
        id: 'categories',
        menuKey: '/categories',
        href: '/categories',
        labelKey: 'nav.categories',
        icon: 'FolderOutlined',
    },
    modifierGroups: {
        id: 'modifierGroups',
        menuKey: '/modifier-groups',
        href: '/modifier-groups',
        labelKey: 'nav.addonGroups',
        icon: 'GroupOutlined',
    },
    pricingRules: {
        id: 'pricingRules',
        menuKey: '/pricing-rules',
        href: '/pricing-rules',
        labelKey: 'nav.priceRules',
        icon: 'TagOutlined',
    },
    inventory: {
        id: 'inventory',
        menuKey: '/inventory',
        href: '/inventory',
        labelKey: 'nav.inventory',
        icon: 'InboxOutlined',
    },
    customers: {
        id: 'customers',
        menuKey: '/customers',
        href: '/customers',
        labelKey: 'nav.customerList',
        icon: 'UserOutlined',
    },
    benefitDefinitions: {
        id: 'benefitDefinitions',
        menuKey: '/benefit-definitions',
        href: '/benefit-definitions',
        labelKey: 'nav.benefitDefinitions',
        icon: 'GiftOutlined',
        sidebarHidden: true,
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
    },
    reportCenter: {
        id: 'reportCenter',
        menuKey: '/reporting/report-center',
        href: '/reporting/report-center',
        labelKey: 'nav.reportCenter',
        icon: 'FundOutlined',
    },
    staffPerformance: {
        id: 'staffPerformance',
        menuKey: '/reporting/staff',
        href: '/reporting/staff',
        labelKey: 'nav.staffPerformance',
        icon: 'TeamOutlined',
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
    },
    tagesbericht: {
        id: 'tagesbericht',
        menuKey: '/reporting/tagesbericht',
        href: '/reporting/tagesbericht',
        labelKey: 'nav.tagesbericht',
        icon: 'FileDoneOutlined',
        sidebarHidden: true,
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
    },
    fiscalExportAuditLogs: {
        id: 'fiscalExportAuditLogs',
        menuKey: '/admin/audit/fiscal-exports',
        href: '/admin/audit/fiscal-exports',
        labelKey: 'nav.fiscalExportAuditLogs',
        icon: 'CloudDownloadOutlined',
    },
    /** Legacy TSE offline intents (`offline_transactions`). New full-order snapshots: RKSV hub → Diagnose → `rksvAdminMenuModel` `/rksv/offline-orders` (`payment.view`). */
    offlineTransactionsAdmin: {
        id: 'offlineTransactionsAdmin',
        menuKey: '/admin/tse/offline-transactions',
        href: '/admin/tse/offline-transactions',
        labelKey: 'nav.offlineTransactionsAdmin',
        icon: 'InboxOutlined',
    },
    users: {
        id: 'users',
        menuKey: '/admin/users',
        href: '/admin/users',
        labelKey: 'nav.users',
        icon: 'TeamOutlined',
        permission: PERMISSIONS.USER_VIEW,
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
        permission: PERMISSIONS.ROLE_VIEW,
    },
    accessMatrix: {
        id: 'accessMatrix',
        menuKey: '/admin/access/matrix',
        href: '/admin/access/matrix',
        labelKey: 'nav.permissionsOverview',
        icon: 'AuditOutlined',
        permission: PERMISSIONS.ROLE_VIEW,
    },
    companySettings: {
        id: 'companySettings',
        menuKey: '/settings/company',
        href: '/settings/company',
        labelKey: 'nav.companyFiscal',
        icon: 'ShopOutlined',
    },
    sessionSettings: {
        id: 'sessionSettings',
        menuKey: '/settings/session',
        href: '/settings/session',
        labelKey: 'nav.sessionInactivity',
        icon: 'ClockCircleOutlined',
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
    },
    paymentMethods: {
        id: 'paymentMethods',
        menuKey: '/settings/payment-methods',
        href: '/settings/payment-methods',
        labelKey: 'nav.paymentMethods',
        icon: 'CreditCardOutlined',
    },
    backupDr: {
        id: 'backupDr',
        menuKey: '/settings/backup-dr',
        href: '/settings/backup-dr',
        labelKey: 'nav.backupDr',
        icon: 'CloudServerOutlined',
        sidebarHidden: true,
    },
    backupMonitoring: {
        id: 'backupMonitoring',
        menuKey: '/admin/backup',
        href: '/admin/backup',
        labelKey: 'nav.backupRestore',
        icon: 'CloudSyncOutlined',
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
    },
    superAdminTenants: {
        id: 'superAdminTenants',
        menuKey: '/admin/tenants',
        href: '/admin/tenants',
        labelKey: 'nav.tenants',
        icon: 'ApartmentOutlined',
    },
    superAdminLicenses: {
        id: 'superAdminLicenses',
        menuKey: '/admin/licenses',
        href: '/admin/licenses',
        labelKey: 'nav.platformLicenses',
        icon: 'SafetyOutlined',
        permission: AppPermissions.LicenseView,
    },
    superAdminCashRegisters: {
        id: 'superAdminCashRegisters',
        menuKey: '/admin/cash-registers',
        href: '/admin/cash-registers',
        labelKey: 'nav.superAdminCashRegisters',
        icon: 'ShopOutlined',
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
    rksvTestsDepExport: {
        id: 'rksvTestsDepExport',
        menuKey: '/admin/rksv/dep-export',
        href: '/admin/rksv/dep-export',
        labelKey: 'nav.depExport',
        icon: 'SafetyCertificateOutlined',
    },
    rksvTestsSignatureVerify: {
        id: 'rksvTestsSignatureVerify',
        menuKey: '/admin/rksv/signature-verify',
        href: '/admin/rksv/signature-verify',
        labelKey: 'nav.signatureVerification',
        icon: 'SafetyCertificateOutlined',
    },
    rksvStatusOverview: {
        id: 'rksvStatusOverview',
        menuKey: '/rksv/operations',
        href: '/rksv',
        labelKey: 'nav.rksvStatus',
        icon: 'SafetyOutlined',
    },
    rksvSystemStatus: {
        id: 'rksvSystemStatus',
        menuKey: '/rksv/status',
        href: '/rksv/status',
        labelKey: 'nav.systemStatus',
        icon: 'ControlOutlined',
    },
    rksvFinanzOnline: {
        id: 'rksvFinanzOnline',
        menuKey: '/rksv/finanz-online-outbox',
        href: '/rksv/finanz-online-outbox',
        labelKey: 'nav.finanzOnline',
        icon: 'CloudSyncOutlined',
    },
    rksvOfflineOrders: {
        id: 'rksvOfflineOrders',
        menuKey: '/rksv/offline-orders',
        href: '/rksv/offline-orders',
        labelKey: 'nav.offlineOrders',
        icon: 'InboxOutlined',
    },
    specialReceiptStart: {
        id: 'specialReceiptStart',
        menuKey: '/rksv/sb/startbeleg',
        href: '/rksv/sonderbelege?focus=startbeleg',
        labelKey: 'nav.startReceipt',
        icon: 'FileDoneOutlined',
    },
    specialReceiptMonthly: {
        id: 'specialReceiptMonthly',
        menuKey: '/rksv/sb/monatsbeleg',
        href: '/rksv/sonderbelege?focus=monatsbeleg',
        labelKey: 'nav.monthlyReceipt',
        icon: 'FileDoneOutlined',
    },
    specialReceiptYearly: {
        id: 'specialReceiptYearly',
        menuKey: '/rksv/sb/jahresbeleg',
        href: '/rksv/sonderbelege?focus=jahresbeleg',
        labelKey: 'nav.yearlyReceipt',
        icon: 'FileDoneOutlined',
    },
    specialReceiptNull: {
        id: 'specialReceiptNull',
        menuKey: '/rksv/sb/nullbeleg',
        href: '/rksv/sonderbelege?focus=nullbeleg',
        labelKey: 'nav.nullReceipt',
        icon: 'FileDoneOutlined',
    },
    specialReceiptClosing: {
        id: 'specialReceiptClosing',
        menuKey: '/rksv/sb/schlussbeleg',
        href: '/rksv/sonderbelege?focus=schlussbeleg',
        labelKey: 'nav.closingReceipt',
        icon: 'FileDoneOutlined',
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
                catalogIds: ['licenseManagement', 'superAdminLicenses', 'billingOverview'],
            },
        ],
    },
    {
        kind: 'group',
        group: 'operations',
        blocks: [
            {
                kind: 'leaves',
                catalogIds: ['operationsCenter', 'tables', 'kassenverwaltung', 'shiftsOverview'],
            },
            {
                kind: 'nested',
                menuKey: ADMIN_SIDEBAR_GROUP_KEYS.salesTransactions,
                labelKey: 'nav.sales',
                icon: 'ShoppingCartOutlined',
                catalogIds: [
                    'receipts',
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
                catalogIds: ['reportingOverview', 'reportCenter', 'userActivityReport', 'staffPerformance'],
            },
        ],
    },
    {
        kind: 'group',
        group: 'settings',
        blocks: [
            {
                kind: 'leaves',
                catalogIds: [
                    'companySettings',
                    'sessionSettings',
                    'personalization',
                    'paymentMethods',
                    'backupMonitoring',
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
                catalogIds: ['accessOverview', 'users', 'accessRoles', 'accessMatrix'],
            },
            { kind: 'leaves', catalogIds: ['superAdminTenants', 'superAdminCashRegisters'] },
        ],
    },
];

export type AdminSidebarComposedData = RksvSidebarRegistryAttachment;

/**
 * Composes RKSV groups via plugin; used by `buildAdminSidebarMenuItems`.
 */
export function composeAdminSidebarData(
    t: (key: string) => string,
    verificationNavLabel: string,
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

/** Catalog + RKSV leaves for guard coverage tests. */
export function getAllSidebarLeafMenuKeysForCoverage(rksvGroups: RksvMenuGroup[]): string[] {
    return [...getSidebarCatalogLeafMenuKeys(), ...FISCAL_RKSV_CLOSING_VIRTUAL_MENU_KEYS, ...collectRksvMenuLeafKeys(rksvGroups)];
}
