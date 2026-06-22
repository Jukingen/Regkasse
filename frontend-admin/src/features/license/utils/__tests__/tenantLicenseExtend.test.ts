import { describe, expect, it } from 'vitest';

import {
    computeExtendedValidUntilUtc,
    maskTenantLicenseKey,
} from '@/features/license/utils/tenantLicenseExtend';

describe('maskTenantLicenseKey', () => {
    it('masks long REGK keys', () => {
        expect(maskTenantLicenseKey('REGK-AAAAA-BBBBB-CCCCC')).toBe('REGK-AAA…CCCC');
    });

    it('returns dash for empty', () => {
        expect(maskTenantLicenseKey(null)).toBe('—');
    });
});

describe('computeExtendedValidUntilUtc', () => {
    it('extends from current valid-until when in the future', () => {
        const now = Date.parse('2026-01-01T12:00:00.000Z');
        const current = '2026-06-01T00:00:00.000Z';
        const result = computeExtendedValidUntilUtc(current, 30, now);
        expect(result.startsWith('2026-07-01')).toBe(true);
    });

    it('extends from now when license already expired', () => {
        const now = Date.parse('2026-06-01T12:00:00.000Z');
        const current = '2026-01-01T00:00:00.000Z';
        const result = computeExtendedValidUntilUtc(current, 90, now);
        expect(result.startsWith('2026-08-30')).toBe(true);
    });
});
