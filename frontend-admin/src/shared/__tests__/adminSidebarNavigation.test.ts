import { describe, expect, it } from 'vitest';
import {
    RKSV_HUB_PATH,
    RKSV_HUB_MENU_LEAF_KEY,
    resolveAdminMenuSelectedKeys,
    getNonRksvSidebarOpenGroupKeys,
    isVerwaltungAdminPath,
    ADMIN_SIDEBAR_GROUP_KEYS,
    normalizeAdminPathname,
    computeSidebarOpenKeysMerge,
    filterSidebarMenuItems,
    type SidebarPermissionContext,
} from '../adminSidebarNavigation';
import { buildRksvMenuGroups } from '../rksvMenuModel';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

const passthroughT = (k: string) => k;
const sampleRksvGroups = () => buildRksvMenuGroups(passthroughT, 'verifications');

const SAMPLE_LEAVES = [
    '/dashboard',
    '/invoices',
    '/receipts',
    '/receipt-templates',
    '/payments',
    '/rksv/finanz-online-queue',
    '/rksv/incident',
];

describe('adminSidebarNavigation', () => {
    it('maps RKSV hub path to operations menu leaf key', () => {
        expect(resolveAdminMenuSelectedKeys(RKSV_HUB_PATH, SAMPLE_LEAVES)).toEqual([RKSV_HUB_MENU_LEAF_KEY]);
    });

    it('maps legacy /rksv/operations pathname to hub leaf when that key is in selectable leaves', () => {
        const leavesWithHub = ['/rksv/operations', '/rksv/finanz-online-queue', '/rksv/incident'];
        expect(resolveAdminMenuSelectedKeys('/rksv/operations', leavesWithHub)).toEqual([RKSV_HUB_MENU_LEAF_KEY]);
    });

    it('normalizes trailing slashes for selected key resolution', () => {
        const leavesWithHub = ['/rksv/operations', '/dashboard'];
        expect(resolveAdminMenuSelectedKeys('/dashboard/', leavesWithHub)).toEqual(['/dashboard']);
        expect(resolveAdminMenuSelectedKeys('/rksv/operations/', leavesWithHub)).toEqual([RKSV_HUB_MENU_LEAF_KEY]);
    });

    it('normalizeAdminPathname strips trailing slashes', () => {
        expect(normalizeAdminPathname('/foo/')).toBe('/foo');
        expect(normalizeAdminPathname('/')).toBe('/');
    });

    it('selects longest matching route prefix for nested paths', () => {
        expect(resolveAdminMenuSelectedKeys('/receipts/abc-uuid', SAMPLE_LEAVES)).toEqual(['/receipts']);
        expect(resolveAdminMenuSelectedKeys('/receipt-templates/new', SAMPLE_LEAVES)).toEqual(['/receipt-templates']);
        expect(resolveAdminMenuSelectedKeys('/rksv/incident', SAMPLE_LEAVES)).toEqual(['/rksv/incident']);
    });

    it('falls back to raw pathname when no leaf matches', () => {
        expect(resolveAdminMenuSelectedKeys('/unknown-route', SAMPLE_LEAVES)).toEqual(['/unknown-route']);
    });

    it('maps Sonderbelege focus query to virtual sidebar keys when present in selectable leaves', () => {
        const leaves = ['/rksv/sonderbelege', '/rksv/sb/startbeleg', '/rksv/sb/schlussbeleg'];
        expect(resolveAdminMenuSelectedKeys('/rksv/sonderbelege', leaves, 'focus=startbeleg')).toEqual([
            '/rksv/sb/startbeleg',
        ]);
        expect(resolveAdminMenuSelectedKeys('/rksv/sonderbelege', leaves, 'focus=schlussbeleg')).toEqual([
            '/rksv/sb/schlussbeleg',
        ]);
        expect(resolveAdminMenuSelectedKeys('/rksv/sonderbelege', leaves, '')).toEqual(['/rksv/sonderbelege']);
    });

    it('returns open group keys for paths under grouped routes', () => {
        expect(getNonRksvSidebarOpenGroupKeys('/dashboard')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.dashboard);
        expect(getNonRksvSidebarOpenGroupKeys('/operations-center')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.operations);
        expect(getNonRksvSidebarOpenGroupKeys('/tables')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.operations);
        expect(getNonRksvSidebarOpenGroupKeys('/payments')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.salesTransactions);
        expect(getNonRksvSidebarOpenGroupKeys('/payments/storno-refund-audit')).toContain(
            ADMIN_SIDEBAR_GROUP_KEYS.salesTransactions,
        );
        expect(getNonRksvSidebarOpenGroupKeys('/products')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.catalog);
        expect(getNonRksvSidebarOpenGroupKeys('/customers')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.customers);
        expect(getNonRksvSidebarOpenGroupKeys('/reporting')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.reports);
        expect(getNonRksvSidebarOpenGroupKeys('/tagesabschluss')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.operations);
        expect(getNonRksvSidebarOpenGroupKeys('/audit-logs')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.rksv);
        expect(getNonRksvSidebarOpenGroupKeys('/admin/audit/fiscal-exports')).toContain(
            ADMIN_SIDEBAR_GROUP_KEYS.rksv,
        );
        expect(getNonRksvSidebarOpenGroupKeys('/admin/tse/offline-transactions')).toContain(
            ADMIN_SIDEBAR_GROUP_KEYS.rksv,
        );
        expect(getNonRksvSidebarOpenGroupKeys('/rksv/incident')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.rksv);
        expect(getNonRksvSidebarOpenGroupKeys('/settings')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.settings);
        expect(getNonRksvSidebarOpenGroupKeys('/settings/payment-methods')).toContain(
            ADMIN_SIDEBAR_GROUP_KEYS.settings,
        );
        expect(getNonRksvSidebarOpenGroupKeys('/admin/tenants')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.admin);
        expect(getNonRksvSidebarOpenGroupKeys('/admin/users')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.accessArea);
    });

    it('detects Verwaltung routes for tenant context card', () => {
        expect(isVerwaltungAdminPath('/admin/users')).toBe(true);
        expect(isVerwaltungAdminPath('/settings/backup-dr')).toBe(true);
        expect(isVerwaltungAdminPath('/admin/tenants')).toBe(true);
        expect(isVerwaltungAdminPath('/products')).toBe(false);
        expect(isVerwaltungAdminPath('/receipts/abc')).toBe(false);
    });
});

describe('filterSidebarMenuItems', () => {
    const baseCtx = (permissions: string[]): SidebarPermissionContext => ({
        usePermissionFirst: true,
        permissions,
        userRole: 'Cashier',
        isMenuItemAllowed,
        canViewUsers: () => false,
        canShowRksvMenu: () => false,
        canShowPlatformAdminMenu: () => false,
        isSuperAdminRole: () => false,
    });

    const sampleMenu = [
        {
            key: 'grp-reporting',
            label: 'Reporting',
            children: [
                { key: '/dashboard', label: 'Dashboard' },
                { key: '/admin/reports', label: 'Reports' },
            ],
        },
        {
            key: 'grp-admin',
            label: 'Admin',
            children: [{ key: '/admin/users', label: 'Users' }],
        },
        {
            key: 'grp-settings',
            label: 'Settings',
            children: [{ key: '/settings/company', label: 'Settings' }],
        },
    ];

    it('hides unauthorized leaves and removes empty parent groups', () => {
        const filtered = filterSidebarMenuItems(sampleMenu, baseCtx([PERMISSIONS.REPORT_VIEW]));
        expect(filtered).toHaveLength(1);
        const reporting = filtered![0] as { children?: { key?: string }[] };
        expect(reporting.children?.map((c) => c.key)).toEqual(['/dashboard', '/admin/reports']);
    });

    it('hides users menu without user.view', () => {
        const filtered = filterSidebarMenuItems(sampleMenu, baseCtx([PERMISSIONS.SETTINGS_VIEW]));
        const adminGroup = filtered!.find((item) => (item as { key?: string }).key === 'grp-admin') as
            | { children?: { key?: string }[] }
            | undefined;
        expect(adminGroup).toBeUndefined();
        const settingsGroup = filtered!.find((item) => (item as { key?: string }).key === 'grp-settings') as
            | { children?: { key?: string }[] }
            | undefined;
        expect(settingsGroup?.children?.map((c) => c.key)).toEqual(['/settings/company']);
    });

    it('shows dashboard for any permission holder', () => {
        const filtered = filterSidebarMenuItems(
            [{ key: '/dashboard', label: 'Dashboard' }],
            baseCtx(['shift.view']),
        );
        expect(filtered).toHaveLength(1);
    });

    it('shows all leaves for SuperAdmin regardless of permission list', () => {
        const filtered = filterSidebarMenuItems(sampleMenu, {
            ...baseCtx([]),
            userRole: 'SuperAdmin',
            isSuperAdminRole: () => true,
        });
        expect(filtered).toHaveLength(3);
        const reporting = filtered![0] as { children?: { key?: string }[] };
        expect(reporting.children?.map((c) => c.key)).toEqual(['/dashboard', '/admin/reports']);
        const admin = filtered![1] as { children?: { key?: string }[] };
        expect(admin.children?.map((c) => c.key)).toEqual(['/admin/users']);
        const settings = filtered![2] as { children?: { key?: string }[] };
        expect(settings.children?.map((c) => c.key)).toEqual(['/settings/company']);
    });
});

describe('computeSidebarOpenKeysMerge', () => {
    const groups = sampleRksvGroups();

    it('prunes RKSV open keys when navigating away from /rksv', () => {
        const prev = [
            ADMIN_SIDEBAR_GROUP_KEYS.rksv,
            '/rksv',
            'rksv-grp-daily',
            ADMIN_SIDEBAR_GROUP_KEYS.reports,
        ];
        const next = computeSidebarOpenKeysMerge({
            pathname: '/dashboard',
            prevOpenKeys: prev,
            canSeeRksv: true,
            rksvGroups: groups,
        });
        expect(next).not.toContain('/rksv');
        expect(next).not.toContain('rksv-grp-daily');
        expect(next).not.toContain(ADMIN_SIDEBAR_GROUP_KEYS.rksv);
        expect(next).toContain(ADMIN_SIDEBAR_GROUP_KEYS.reports);
    });

    it('opens fiscal + RKSV subtree on deep link when user may see RKSV', () => {
        const next = computeSidebarOpenKeysMerge({
            pathname: '/rksv/incident',
            prevOpenKeys: [],
            canSeeRksv: true,
            rksvGroups: groups,
        });
        expect(next).toContain(ADMIN_SIDEBAR_GROUP_KEYS.rksv);
        expect(next).toContain('/rksv');
        expect(next).toContain('rksv-grp-investigation');
    });

    it('does not keep RKSV open on /rksv when user cannot see RKSV', () => {
        const next = computeSidebarOpenKeysMerge({
            pathname: '/rksv/incident',
            prevOpenKeys: [],
            canSeeRksv: false,
            rksvGroups: groups,
        });
        expect(next).not.toContain(ADMIN_SIDEBAR_GROUP_KEYS.rksv);
        expect(next).not.toContain('/rksv');
    });

    it('opens RKSV group for audit without RKSV hub keys', () => {
        const next = computeSidebarOpenKeysMerge({
            pathname: '/audit-logs',
            prevOpenKeys: [],
            canSeeRksv: true,
            rksvGroups: groups,
        });
        expect(next).toContain(ADMIN_SIDEBAR_GROUP_KEYS.rksv);
        expect(next).not.toContain('/rksv');
    });

    it('opens RKSV group for fiscal-export audit route', () => {
        const next = computeSidebarOpenKeysMerge({
            pathname: '/admin/audit/fiscal-exports',
            prevOpenKeys: [],
            canSeeRksv: true,
            rksvGroups: groups,
        });
        expect(next).toContain(ADMIN_SIDEBAR_GROUP_KEYS.rksv);
        expect(next).not.toContain('/rksv');
    });

    it('opens special-receipts subgroup for Sonderbelege without expanding the RKSV hub', () => {
        const next = computeSidebarOpenKeysMerge({
            pathname: '/rksv/sonderbelege',
            prevOpenKeys: [],
            canSeeRksv: true,
            rksvGroups: groups,
        });
        expect(next).toContain(ADMIN_SIDEBAR_GROUP_KEYS.rksv);
        expect(next).toContain(ADMIN_SIDEBAR_GROUP_KEYS.specialReceipts);
        expect(next).not.toContain('/rksv');
    });
});
