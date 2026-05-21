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
    | 'CloudServerOutlined'
    | 'ExperimentOutlined'
    | 'WalletOutlined'
    | 'ClockCircleOutlined'
    | 'CloudDownloadOutlined'
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
    },
    companySettings: {
        id: 'companySettings',
        menuKey: '/settings',
        href: '/settings',
        labelKey: 'nav.companySettings',
        icon: 'ShopOutlined',
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
        labelKey: 'nav.licenseManagement',
        icon: 'KeyOutlined',
    },
    superAdminTenants: {
        id: 'superAdminTenants',
        menuKey: '/admin/tenants',
        href: '/admin/tenants',
        labelKey: 'nav.superAdminTenants',
        icon: 'ApartmentOutlined',
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
    | { kind: 'domain'; domain: SidebarDomainId; blocks: SidebarLayoutBlock[] };

/**
 * Top-to-bottom shell layout (order = render order).
 */
export const SIDEBAR_LAYOUT_ROWS: SidebarLayoutRow[] = [
    {
        kind: 'leaves',
        catalogIds: ['reportingDashboard', 'users', 'superAdminTenants'],
    },
    { kind: 'divider', key: 'nav-divider-platform-hub' },
    {
        kind: 'domain',
        domain: 'operations',
        blocks: [{ kind: 'leaves', catalogIds: ['operationsCenter', 'tables', 'kassenverwaltung'] }],
    },
    {
        kind: 'domain',
        domain: 'sales',
        blocks: [
            {
                kind: 'leaves',
                catalogIds: ['receipts', 'payments', 'stornoRefundAudit', 'vouchers', 'invoices', 'receiptTemplates', 'receiptGenerate'],
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
                catalogIds: ['reportingOverview', 'reportCenter', 'staffPerformance', 'dailyClosingSummary'],
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
                menuKey: ADMIN_SIDEBAR_GROUP_KEYS.settingsArea,
                labelKey: 'nav.settingsHub',
                icon: 'SettingOutlined',
                catalogIds: ['companySettings', 'paymentMethods', 'backupDr', 'developmentMode', 'timeSync', 'licenseManagement'],
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

/** All catalog leaf `menuKey` values (for permission / route drift tests). */
export function getSidebarCatalogLeafMenuKeys(): string[] {
    return Object.values(SIDEBAR_NAV_ITEM_CATALOG).map((item) => item.menuKey);
}

/** Catalog + RKSV leaves for guard coverage tests. */
export function getAllSidebarLeafMenuKeysForCoverage(rksvGroups: RksvMenuGroup[]): string[] {
    return [...getSidebarCatalogLeafMenuKeys(), ...FISCAL_RKSV_CLOSING_VIRTUAL_MENU_KEYS, ...collectRksvMenuLeafKeys(rksvGroups)];
}
