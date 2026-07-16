import { describe, expect, it } from 'vitest';
import { SIDEBAR_NAV_ITEM_CATALOG, SIDEBAR_LAYOUT_ROWS } from '@/shared/adminSidebarRegistry';
import { ROUTE_PERMISSIONS } from '@/shared/auth/routePermissions';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';
import { AppPermissions, PERMISSIONS } from '@/shared/auth/permissions';
import { MANAGER_ADMIN_PERMISSIONS } from '@/shared/__tests__/fixtures/adminAppPermissionFixtures';

describe('sidebarRegistryCatalog', () => {
    it('references only defined catalog ids in layout rows', () => {
        const ids = new Set(Object.keys(SIDEBAR_NAV_ITEM_CATALOG));

        for (const row of SIDEBAR_LAYOUT_ROWS) {
            if (row.kind === 'leaves' || row.kind === 'nested') {
                for (const id of row.catalogIds) {
                    expect(ids.has(id), `Unknown catalog id: ${id}`).toBe(true);
                }
                continue;
            }
            if (row.kind !== 'group') continue;
            for (const block of row.blocks) {
                if (block.kind === 'leaves' || block.kind === 'nested') {
                    for (const id of block.catalogIds) {
                        expect(ids.has(id), `Unknown catalog id: ${id}`).toBe(true);
                    }
                }
            }
        }
    });

    it('uses unique menuKey per catalog item', () => {
        const keys = Object.values(SIDEBAR_NAV_ITEM_CATALOG).map((x) => x.menuKey);
        expect(new Set(keys).size).toBe(keys.length);
    });

    it('aligns catalog permission with ROUTE_PERMISSIONS when declared', () => {
        for (const item of Object.values(SIDEBAR_NAV_ITEM_CATALOG)) {
            if (item.permission === undefined) continue;
            expect(ROUTE_PERMISSIONS[item.menuKey], item.menuKey).toEqual(item.permission);
        }
    });

    it('shows dashboard for any user with permission claims', () => {
        expect(isMenuItemAllowed('/dashboard', ['product.view'])).toBe(true);
        expect(isMenuItemAllowed('/dashboard', [])).toBe(false);
    });

    it('hides Kassenverwaltung without cash_register.manage', () => {
        expect(
            isMenuItemAllowed('/kassenverwaltung', [AppPermissions.CashRegisterManage]),
        ).toBe(true);
        expect(isMenuItemAllowed('/kassenverwaltung', [AppPermissions.CashRegisterView])).toBe(false);
        expect(isMenuItemAllowed('/kassenverwaltung', ['product.view'])).toBe(false);
        expect(isMenuItemAllowed('/kassenverwaltung', [])).toBe(false);
    });

    it('hides Super Admin-only sidebar leaves from Manager permissions', () => {
        const managerPerms = [...MANAGER_ADMIN_PERMISSIONS];
        for (const key of ['/admin/tenants', '/admin/billing', '/admin/cash-registers', '/admin/licenses']) {
            expect(isMenuItemAllowed(key, managerPerms), key).toBe(false);
        }
        expect(isMenuItemAllowed('/admin/license', managerPerms)).toBe(true);
    });

    it('declares system.critical on Super Admin platform catalog leaves', () => {
        expect(SIDEBAR_NAV_ITEM_CATALOG.superAdminTenants.permission).toBe(PERMISSIONS.SYSTEM_CRITICAL);
        expect(SIDEBAR_NAV_ITEM_CATALOG.billingOverview.permission).toEqual([PERMISSIONS.SYSTEM_CRITICAL]);
        expect(SIDEBAR_NAV_ITEM_CATALOG.superAdminCashRegisters.permission).toBe(PERMISSIONS.SYSTEM_CRITICAL);
    });

    it('hides RKSV test helper from Manager sidebar permissions', () => {
        const managerPerms = [...MANAGER_ADMIN_PERMISSIONS];
        expect(isMenuItemAllowed('/rksv/sb/test-helper', managerPerms)).toBe(false);
        expect(SIDEBAR_NAV_ITEM_CATALOG.specialReceiptTestHelper.permission).toBe(PERMISSIONS.SYSTEM_CRITICAL);
    });
});
