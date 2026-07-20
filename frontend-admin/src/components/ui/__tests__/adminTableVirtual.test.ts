import { describe, expect, it } from 'vitest';
import {
    ADMIN_TABLE_VIRTUAL_MIN_ROWS,
    ADMIN_TABLE_VIRTUAL_SCROLL_Y,
    adminTableScrollXy,
    shouldUseAdminTableVirtual,
} from '@/components/ui/adminTableVirtual';

describe('adminTableVirtual', () => {
    it('enables virtual at or above the min row threshold', () => {
        expect(shouldUseAdminTableVirtual(ADMIN_TABLE_VIRTUAL_MIN_ROWS - 1)).toBe(false);
        expect(shouldUseAdminTableVirtual(ADMIN_TABLE_VIRTUAL_MIN_ROWS)).toBe(true);
        expect(shouldUseAdminTableVirtual(100)).toBe(true);
    });

    it('adds scroll.y only when virtual should apply', () => {
        expect(adminTableScrollXy(1200, 10)).toEqual({ x: 1200 });
        expect(adminTableScrollXy(1200, ADMIN_TABLE_VIRTUAL_MIN_ROWS)).toEqual({
            x: 1200,
            y: ADMIN_TABLE_VIRTUAL_SCROLL_Y,
        });
    });
});
