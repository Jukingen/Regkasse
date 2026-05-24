import { describe, expect, it } from 'vitest';
import { SIDEBAR_NAV_ITEM_CATALOG, SIDEBAR_LAYOUT_ROWS } from '@/shared/adminSidebarRegistry';
import { ROUTE_PERMISSIONS } from '@/shared/auth/routePermissions';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';
import { AppPermissions } from '@/shared/auth/permissions';

describe('sidebarRegistryCatalog', () => {
    it('references only defined catalog ids in layout rows', () => {
        const ids = new Set(Object.keys(SIDEBAR_NAV_ITEM_CATALOG));

        for (const row of SIDEBAR_LAYOUT_ROWS) {
            if (row.kind === 'leaves') {
                for (const id of row.catalogIds) {
                    expect(ids.has(id), `Unknown catalog id: ${id}`).toBe(true);
                }
                continue;
            }
            if (row.kind !== 'domain') continue;
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
            expect(ROUTE_PERMISSIONS[item.menuKey], item.menuKey).toBe(item.permission);
        }
    });

    it('hides Kassenverwaltung without cash_register.view', () => {
        expect(
            isMenuItemAllowed('/kassenverwaltung', [AppPermissions.CashRegisterView]),
        ).toBe(true);
        expect(isMenuItemAllowed('/kassenverwaltung', ['product.view'])).toBe(false);
        expect(isMenuItemAllowed('/kassenverwaltung', [])).toBe(false);
    });
});
