import { describe, expect, it } from 'vitest';
import { SIDEBAR_NAV_ITEM_CATALOG, SIDEBAR_LAYOUT_ROWS } from '@/shared/adminSidebarRegistry';

describe('sidebarRegistryCatalog', () => {
    it('references only defined catalog ids in layout rows', () => {
        const ids = new Set(Object.keys(SIDEBAR_NAV_ITEM_CATALOG));

        for (const row of SIDEBAR_LAYOUT_ROWS) {
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
});
