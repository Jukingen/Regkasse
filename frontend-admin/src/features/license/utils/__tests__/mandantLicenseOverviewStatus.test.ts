import { describe, expect, it } from 'vitest';

import { resolveMandantLicenseOverviewStatus } from '@/features/license/utils/mandantLicenseOverviewStatus';

const NOW = Date.parse('2026-06-22T12:00:00.000Z');

describe('resolveMandantLicenseOverviewStatus', () => {
    it('returns none when no key and no end date', () => {
        expect(resolveMandantLicenseOverviewStatus(null, null, null, NOW)).toEqual({
            kind: 'none',
            daysRemaining: null,
        });
    });

    it('returns trial when end date exists without key', () => {
        const until = new Date(NOW + 20 * 24 * 60 * 60 * 1000).toISOString();
        expect(resolveMandantLicenseOverviewStatus(until, null, 20, NOW)).toEqual({
            kind: 'trial',
            daysRemaining: 20,
        });
    });

    it('returns expired when end date is in the past', () => {
        const until = new Date(NOW - 2 * 24 * 60 * 60 * 1000).toISOString();
        expect(resolveMandantLicenseOverviewStatus(until, 'REGK-AAAA-BBBB-CCCC', -2, NOW)).toEqual({
            kind: 'expired',
            daysRemaining: -2,
        });
    });

    it('returns expiring_soon when licensed and within 7 days', () => {
        const until = new Date(NOW + 5 * 24 * 60 * 60 * 1000).toISOString();
        expect(resolveMandantLicenseOverviewStatus(until, 'REGK-AAAA-BBBB-CCCC', 5, NOW)).toEqual({
            kind: 'expiring_soon',
            daysRemaining: 5,
        });
    });

    it('returns active when licensed and more than 7 days remain', () => {
        const until = new Date(NOW + 30 * 24 * 60 * 60 * 1000).toISOString();
        expect(resolveMandantLicenseOverviewStatus(until, 'REGK-AAAA-BBBB-CCCC', 30, NOW)).toEqual({
            kind: 'active',
            daysRemaining: 30,
        });
    });
});
