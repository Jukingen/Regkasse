import { describe, expect, it } from 'vitest';
import {
    RKSV_HUB_PATH,
    RKSV_HUB_MENU_LEAF_KEY,
    resolveAdminMenuSelectedKeys,
    getNonRksvSidebarOpenGroupKeys,
    ADMIN_SIDEBAR_GROUP_KEYS,
} from '../adminSidebarNavigation';

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

    it('selects longest matching route prefix for nested paths', () => {
        expect(resolveAdminMenuSelectedKeys('/receipts/abc-uuid', SAMPLE_LEAVES)).toEqual(['/receipts']);
        expect(resolveAdminMenuSelectedKeys('/receipt-templates/new', SAMPLE_LEAVES)).toEqual(['/receipt-templates']);
        expect(resolveAdminMenuSelectedKeys('/rksv/incident', SAMPLE_LEAVES)).toEqual(['/rksv/incident']);
    });

    it('falls back to raw pathname when no leaf matches', () => {
        expect(resolveAdminMenuSelectedKeys('/unknown-route', SAMPLE_LEAVES)).toEqual(['/unknown-route']);
    });

    it('returns open group keys for paths under grouped routes', () => {
        expect(getNonRksvSidebarOpenGroupKeys('/operations-center')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.kasseBelege);
        expect(getNonRksvSidebarOpenGroupKeys('/tables')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.kasseBelege);
        expect(getNonRksvSidebarOpenGroupKeys('/payments')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.kasseBelege);
        expect(getNonRksvSidebarOpenGroupKeys('/products')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.sortiment);
        expect(getNonRksvSidebarOpenGroupKeys('/customers')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.kundenVorteile);
        expect(getNonRksvSidebarOpenGroupKeys('/settings')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.verwaltung);
        expect(getNonRksvSidebarOpenGroupKeys('/settings')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.settingsArea);
        expect(getNonRksvSidebarOpenGroupKeys('/settings/payment-methods')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.verwaltung);
        expect(getNonRksvSidebarOpenGroupKeys('/settings/payment-methods')).toContain(ADMIN_SIDEBAR_GROUP_KEYS.settingsArea);
        expect(getNonRksvSidebarOpenGroupKeys('/rksv/incident')).toEqual([]);
    });
});
