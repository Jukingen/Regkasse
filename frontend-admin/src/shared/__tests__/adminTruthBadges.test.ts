import { describe, expect, it } from 'vitest';
import {
    ADMIN_TRUTH_BADGE,
    ADMIN_TRUTH_BADGE_KINDS,
    adminTruthTooltip,
} from '@/shared/adminTruthBadges';

describe('adminTruthBadges', () => {
    it('defines copy for every kind', () => {
        for (const kind of ADMIN_TRUTH_BADGE_KINDS) {
            const row = ADMIN_TRUTH_BADGE[kind];
            expect(row.shortLabel.length).toBeGreaterThan(0);
            expect(row.tooltip.length).toBeGreaterThan(10);
            expect(row.antColor.length).toBeGreaterThan(0);
            expect(adminTruthTooltip(kind)).toBe(row.tooltip);
        }
    });

    it('does not use success green for truth lineage (operational clarity)', () => {
        for (const kind of ADMIN_TRUTH_BADGE_KINDS) {
            expect(ADMIN_TRUTH_BADGE[kind].antColor).not.toBe('success');
        }
    });
});
