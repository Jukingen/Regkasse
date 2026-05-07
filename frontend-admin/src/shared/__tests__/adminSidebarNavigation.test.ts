import { describe, expect, it } from 'vitest';
import {
    RKSV_HUB_PATH,
    RKSV_HUB_MENU_LEAF_KEY,
    resolveAdminMenuSelectedKeys,
    getNonRksvSidebarOpenGroupKeys,
    ADMIN_SIDEBAR_GROUP_KEYS,
    normalizeAdminPathname,
    computeSidebarOpenKeysMerge,
} from '../adminSidebarNavigation';
import { buildRksvMenuGroups } from '../rksvMenuModel';

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
        expect(getNonRksvSidebarOpenGroupKeys('/operations-center')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.operations);
        expect(getNonRksvSidebarOpenGroupKeys('/tables')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.operations);
        expect(getNonRksvSidebarOpenGroupKeys('/payments')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.salesTransactions);
        expect(getNonRksvSidebarOpenGroupKeys('/payments/storno-refund-audit')).toContain(
            ADMIN_SIDEBAR_GROUP_KEYS.salesTransactions,
        );
        expect(getNonRksvSidebarOpenGroupKeys('/products')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.catalogPricing);
        expect(getNonRksvSidebarOpenGroupKeys('/customers')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.customersBenefits);
        expect(getNonRksvSidebarOpenGroupKeys('/reporting/tagesbericht')).toContain(
            ADMIN_SIDEBAR_GROUP_KEYS.reportingAnalytics,
        );
        expect(getNonRksvSidebarOpenGroupKeys('/reporting/tagesbericht')).toContain(
            ADMIN_SIDEBAR_GROUP_KEYS.fiscalRksvClosing,
        );
        expect(getNonRksvSidebarOpenGroupKeys('/tagesabschluss')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.fiscalRksvClosing);
        expect(getNonRksvSidebarOpenGroupKeys('/audit-logs')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.fiscalCompliance);
        expect(getNonRksvSidebarOpenGroupKeys('/admin/audit/fiscal-exports')).toContain(
            ADMIN_SIDEBAR_GROUP_KEYS.fiscalCompliance,
        );
        expect(getNonRksvSidebarOpenGroupKeys('/admin/tse/offline-transactions')).toContain(
            ADMIN_SIDEBAR_GROUP_KEYS.fiscalCompliance,
        );
        expect(getNonRksvSidebarOpenGroupKeys('/rksv/incident')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.fiscalCompliance);
        expect(getNonRksvSidebarOpenGroupKeys('/settings')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.verwaltung);
        expect(getNonRksvSidebarOpenGroupKeys('/settings')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.settingsArea);
        expect(getNonRksvSidebarOpenGroupKeys('/settings/payment-methods')).toContain(
            ADMIN_SIDEBAR_GROUP_KEYS.verwaltung,
        );
        expect(getNonRksvSidebarOpenGroupKeys('/settings/payment-methods')).toContain(
            ADMIN_SIDEBAR_GROUP_KEYS.settingsArea,
        );
    });
});

describe('computeSidebarOpenKeysMerge', () => {
    const groups = sampleRksvGroups();

    it('prunes RKSV open keys when navigating away from /rksv', () => {
        const prev = [
            ADMIN_SIDEBAR_GROUP_KEYS.fiscalCompliance,
            '/rksv',
            'rksv-grp-daily',
            ADMIN_SIDEBAR_GROUP_KEYS.reportingAnalytics,
        ];
        const next = computeSidebarOpenKeysMerge({
            pathname: '/dashboard',
            prevOpenKeys: prev,
            canSeeRksv: true,
            rksvGroups: groups,
        });
        expect(next).not.toContain('/rksv');
        expect(next).not.toContain('rksv-grp-daily');
        expect(next).not.toContain(ADMIN_SIDEBAR_GROUP_KEYS.fiscalCompliance);
        expect(next).toContain(ADMIN_SIDEBAR_GROUP_KEYS.reportingAnalytics);
    });

    it('opens fiscal + RKSV subtree on deep link when user may see RKSV', () => {
        const next = computeSidebarOpenKeysMerge({
            pathname: '/rksv/incident',
            prevOpenKeys: [],
            canSeeRksv: true,
            rksvGroups: groups,
        });
        expect(next).toContain(ADMIN_SIDEBAR_GROUP_KEYS.fiscalCompliance);
        expect(next).toContain('/rksv');
        expect(next).toContain('rksv-grp-investigation');
    });

    it('does not keep fiscal open on /rksv when user cannot see RKSV', () => {
        const next = computeSidebarOpenKeysMerge({
            pathname: '/rksv/incident',
            prevOpenKeys: [],
            canSeeRksv: false,
            rksvGroups: groups,
        });
        expect(next).not.toContain(ADMIN_SIDEBAR_GROUP_KEYS.fiscalCompliance);
        expect(next).not.toContain('/rksv');
    });

    it('opens fiscal for audit without RKSV keys', () => {
        const next = computeSidebarOpenKeysMerge({
            pathname: '/audit-logs',
            prevOpenKeys: [],
            canSeeRksv: true,
            rksvGroups: groups,
        });
        expect(next).toContain(ADMIN_SIDEBAR_GROUP_KEYS.fiscalCompliance);
        expect(next).not.toContain('/rksv');
    });

    it('opens fiscal for fiscal-export audit route', () => {
        const next = computeSidebarOpenKeysMerge({
            pathname: '/admin/audit/fiscal-exports',
            prevOpenKeys: [],
            canSeeRksv: true,
            rksvGroups: groups,
        });
        expect(next).toContain(ADMIN_SIDEBAR_GROUP_KEYS.fiscalCompliance);
        expect(next).not.toContain('/rksv');
    });

    it('opens fiscal closing subgroup for Sonderbelege without expanding the RKSV hub', () => {
        const next = computeSidebarOpenKeysMerge({
            pathname: '/rksv/sonderbelege',
            prevOpenKeys: [],
            canSeeRksv: true,
            rksvGroups: groups,
        });
        expect(next).toContain(ADMIN_SIDEBAR_GROUP_KEYS.fiscalCompliance);
        expect(next).toContain(ADMIN_SIDEBAR_GROUP_KEYS.fiscalRksvClosing);
        expect(next).not.toContain('/rksv');
    });
});
