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

export type SidebarDomainId =
    | 'operations'
    | 'sales'
    | 'catalog'
    | 'customers'
    | 'reporting'
    | 'fiscalCompliance'
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
    | 'ApartmentOutlined';

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
        labelKey: 'nav.kassenverwaltung',
        icon: 'ShopOutlined',
        permission: AppPermissions.CashRegisterManage,
    },
    shiftsOverview: {
        id: 'shiftsOverview',
        menuKey: '/shifts',
        href: '/shifts',
        labelKey: 'nav.shiftsOverview',
        icon: 'ClockCircleOutlined',
        permission: PERMISSIONS.SHIFT_VIEW,
    },
    tagesabschluss: {
        id: 'tagesabschluss',
        menuKey: '/tagesabschluss',
        href: '/tagesabschluss',
        labelKey: 'nav.tagesabschluss',
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
        labelKey: 'nav.stornoRefundAudit',
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
    },
    receiptGenerate: {
        id: 'receiptGenerate',
        menuKey: '/receipt-generate',
        href: '/receipt-generate',
        labelKey: 'nav.receiptGenerate',
        icon: 'EyeOutlined',
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
        labelKey: 'nav.modifierGroups',
        icon: 'GroupOutlined',
    },
    pricingRules: {
        id: 'pricingRules',
        menuKey: '/pricing-rules',
        href: '/pricing-rules',
        labelKey: 'nav.pricingRules',
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
        labelKey: 'nav.customers',
        icon: 'UserOutlined',
    },
    benefitDefinitions: {
        id: 'benefitDefinitions',
        menuKey: '/benefit-definitions',
        href: '/benefit-definitions',
        labelKey: 'nav.benefitDefinitions',
        icon: 'GiftOutlined',
    },
    benefitAssignments: {
        id: 'benefitAssignments',
        menuKey: '/benefit-assignments',
        href: '/benefit-assignments',
        labelKey: 'nav.benefitAssignments',
        icon: 'TagOutlined',
    },
    reportingDashboard: {
        id: 'reportingDashboard',
        menuKey: '/dashboard',
        href: '/dashboard',
        labelKey: 'nav.reportingDashboard',
        icon: 'DashboardOutlined',
        permission: ANY_AUTHENTICATED_PERMISSION,
    },
    reportingOverview: {
        id: 'reportingOverview',
        menuKey: '/reporting',
        href: '/reporting',
        labelKey: 'nav.reporting',
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
    },
    complianceReports: {
        id: 'complianceReports',
        menuKey: '/reporting/compliance',
        href: '/reporting/compliance',
        labelKey: 'nav.complianceReports',
        icon: 'SafetyCertificateOutlined',
    },
    userActivityReport: {
        id: 'userActivityReport',
        menuKey: '/admin/reports/user-activity',
        href: '/admin/reports/user-activity',
        labelKey: 'nav.userActivityReport',
        icon: 'TeamOutlined',
    },
    tagesbericht: {
        id: 'tagesbericht',
        menuKey: '/reporting/tagesbericht',
        href: '/reporting/tagesbericht',
        labelKey: 'nav.tagesbericht',
        icon: 'FileDoneOutlined',
    },
    monatsbericht: {
        id: 'monatsbericht',
        menuKey: '/reporting/monatsbericht',
        href: '/reporting/monatsbericht',
        labelKey: 'nav.monatsbericht',
        icon: 'BarChartOutlined',
    },
    jahresbericht: {
        id: 'jahresbericht',
        menuKey: '/reporting/jahresbericht',
        href: '/reporting/jahresbericht',
        labelKey: 'nav.jahresbericht',
        icon: 'AreaChartOutlined',
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
        labelKey: 'nav.accessOverview',
        icon: 'KeyOutlined',
        permission: PERMISSIONS.USER_VIEW,
    },
    accessRoles: {
        id: 'accessRoles',
        menuKey: '/admin/access/roles',
        href: '/admin/access/roles',
        labelKey: 'nav.accessRoles',
        icon: 'SafetyOutlined',
        permission: PERMISSIONS.ROLE_VIEW,
    },
    accessMatrix: {
        id: 'accessMatrix',
        menuKey: '/admin/access/matrix',
        href: '/admin/access/matrix',
        labelKey: 'nav.accessMatrix',
        icon: 'AuditOutlined',
        permission: PERMISSIONS.ROLE_VIEW,
    },
    companySettings: {
        id: 'companySettings',
        menuKey: '/settings/company',
        href: '/settings/company',
        labelKey: 'nav.companySettings',
        icon: 'ShopOutlined',
    },
    sessionSettings: {
        id: 'sessionSettings',
        menuKey: '/settings/session',
        href: '/settings/session',
        labelKey: 'nav.sessionSettings',
        icon: 'ClockCircleOutlined',
    },
    personalization: {
        id: 'personalization',
        menuKey: '/settings/personalization',
        href: '/settings/personalization',
        labelKey: 'nav.personalization',
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
    },
    timeSync: {
        id: 'timeSync',
        menuKey: '/admin/system/time-sync',
        href: '/admin/system/time-sync',
        labelKey: 'nav.timeSync',
        icon: 'ClockCircleOutlined',
    },
    licenseManagement: {
        id: 'licenseManagement',
        menuKey: '/admin/license',
        href: '/admin/license',
        labelKey: 'nav.licenseHubLicenses',
        icon: 'KeyOutlined',
    },
    superAdminTenants: {
        id: 'superAdminTenants',
        menuKey: '/admin/tenants',
        href: '/admin/tenants',
        labelKey: 'nav.superAdminTenants',
        icon: 'ApartmentOutlined',
    },
    superAdminLicenses: {
        id: 'superAdminLicenses',
        menuKey: '/admin/licenses',
        href: '/admin/licenses',
        labelKey: 'nav.superAdminLicenses',
        icon: 'SafetyOutlined',
        permission: AppPermissions.LicenseView,
    },
    superAdminCashRegisters: {
        id: 'superAdminCashRegisters',
        menuKey: '/admin/cash-registers',
        href: '/admin/cash-registers',
        labelKey: 'nav.superAdminCashRegisters',
        icon: 'ShopOutlined',
    },
    billingOverview: {
        id: 'billingOverview',
        menuKey: '/admin/billing',
        href: '/admin/billing',
        labelKey: 'nav.licenseHubSales',
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
        labelKey: 'nav.rksvTestsDepExport',
        icon: 'SafetyCertificateOutlined',
    },
    rksvTestsSignatureVerify: {
        id: 'rksvTestsSignatureVerify',
        menuKey: '/admin/rksv/signature-verify',
        href: '/admin/rksv/signature-verify',
        labelKey: 'nav.rksvTestsSignatureVerify',
        icon: 'SafetyCertificateOutlined',
    },
};

export const SIDEBAR_DOMAIN_GROUP_META: Record<
    SidebarDomainId,
    { menuKey: string; labelKey: string; icon: SidebarIconToken }
> = {
    operations: {
        menuKey: ADMIN_SIDEBAR_GROUP_KEYS.operations,
        labelKey: 'adminShell.group.operations',
        icon: 'ThunderboltOutlined',
    },
    sales: {
        menuKey: ADMIN_SIDEBAR_GROUP_KEYS.salesTransactions,
        labelKey: 'adminShell.group.salesTransactions',
        icon: 'ShoppingCartOutlined',
    },
    catalog: {
        menuKey: ADMIN_SIDEBAR_GROUP_KEYS.catalogPricing,
        labelKey: 'adminShell.group.catalogPricing',
        icon: 'AppstoreOutlined',
    },
    customers: {
        menuKey: ADMIN_SIDEBAR_GROUP_KEYS.customersBenefits,
        labelKey: 'adminShell.group.customersBenefits',
        icon: 'UsergroupAddOutlined',
    },
    reporting: {
        menuKey: ADMIN_SIDEBAR_GROUP_KEYS.reportingAnalytics,
        labelKey: 'adminShell.group.reportingAnalytics',
        icon: 'LineChartOutlined',
    },
    fiscalCompliance: {
        menuKey: ADMIN_SIDEBAR_GROUP_KEYS.fiscalCompliance,
        labelKey: 'adminShell.group.fiscalCompliance',
        icon: 'AuditOutlined',
    },
    administration: {
        menuKey: ADMIN_SIDEBAR_GROUP_KEYS.verwaltung,
        labelKey: 'adminShell.group.verwaltung',
        icon: 'ToolOutlined',
    },
};

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
    | { kind: 'domain'; domain: SidebarDomainId; blocks: SidebarLayoutBlock[] };

/**
 * Top-to-bottom shell layout (order = render order).
 */
export const SIDEBAR_LAYOUT_ROWS: SidebarLayoutRow[] = [
    {
        kind: 'leaves',
        catalogIds: [
            'reportingDashboard',
            'superAdminTenants',
            'superAdminLicenses',
            'superAdminCashRegisters',
        ],
    },
    {
        kind: 'nested',
        menuKey: ADMIN_SIDEBAR_GROUP_KEYS.license,
        labelKey: 'nav.licenseHub',
        icon: 'CreditCardOutlined',
        catalogIds: ['licenseManagement', 'billingOverview', 'billingSales', 'billingSalesNew', 'billingStats'],
    },
    { kind: 'divider', key: 'nav-divider-platform-hub' },
    {
        kind: 'domain',
        domain: 'operations',
        blocks: [{ kind: 'leaves', catalogIds: ['operationsCenter', 'tables', 'kassenverwaltung', 'shiftsOverview'] }],
    },
    {
        kind: 'domain',
        domain: 'sales',
        blocks: [
            {
                kind: 'leaves',
                catalogIds: ['receipts', 'payments', 'paymentTrends', 'cardTransactions', 'stornoRefundAudit', 'vouchers', 'invoices', 'receiptTemplates', 'receiptGenerate'],
            },
        ],
    },
    {
        kind: 'domain',
        domain: 'catalog',
        blocks: [
            {
                kind: 'leaves',
                catalogIds: ['products', 'categories', 'modifierGroups', 'pricingRules', 'inventory'],
            },
        ],
    },
    {
        kind: 'domain',
        domain: 'customers',
        blocks: [
            {
                kind: 'leaves',
                catalogIds: ['customers', 'benefitDefinitions', 'benefitAssignments'],
            },
        ],
    },
    { kind: 'divider', key: 'nav-divider-reporting' },
    {
        kind: 'domain',
        domain: 'reporting',
        blocks: [
            {
                kind: 'leaves',
                catalogIds: [
                    'reportingOverview',
                    'reportCenter',
                    'complianceReports',
                    'userActivityReport',
                    'staffPerformance',
                    'dailyClosingSummary',
                ],
            },
        ],
    },
    { kind: 'divider', key: 'nav-divider-fiscal' },
    {
        kind: 'domain',
        domain: 'fiscalCompliance',
        blocks: [
            {
                kind: 'fiscalRksvClosing',
                menuKey: ADMIN_SIDEBAR_GROUP_KEYS.fiscalRksvClosing,
                labelKey: 'nav.fiscalRksvClosingHub',
                icon: 'SafetyOutlined',
            },
            { kind: 'leaves', catalogIds: ['auditLogs', 'fiscalExportAuditLogs', 'offlineTransactionsAdmin'] },
            {
                kind: 'nested',
                menuKey: '/admin/rksv',
                labelKey: 'nav.rksvTestsHub',
                icon: 'SafetyCertificateOutlined',
                catalogIds: ['rksvTestsDepExport', 'rksvTestsSignatureVerify'],
            },
            {
                kind: 'rksvHub',
                menuKey: '/rksv',
                labelKey: 'adminShell.group.rksv',
                icon: 'SafetyOutlined',
            },
        ],
    },
    { kind: 'divider', key: 'nav-divider-admin' },
    {
        kind: 'domain',
        domain: 'administration',
        blocks: [
            {
                kind: 'nested',
                menuKey: ADMIN_SIDEBAR_GROUP_KEYS.accessArea,
                labelKey: 'nav.accessHub',
                icon: 'KeyOutlined',
                catalogIds: ['accessOverview', 'users', 'accessRoles', 'accessMatrix'],
            },
            {
                kind: 'nested',
                menuKey: ADMIN_SIDEBAR_GROUP_KEYS.settingsArea,
                labelKey: 'nav.settingsHub',
                icon: 'SettingOutlined',
                catalogIds: ['companySettings', 'sessionSettings', 'personalization', 'paymentMethods', 'backupMonitoring', 'backupDr', 'developmentMode', 'timeSync'],
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
