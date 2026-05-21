import { describe, expect, it } from 'vitest';

import { resolveTenantLicenseLabel } from '../tenantLicenseLabel';

describe('resolveTenantLicenseLabel', () => {
    const now = new Date('2026-05-20T12:00:00Z').getTime();

    it('returns none when no license end date', () => {
        expect(resolveTenantLicenseLabel(null, null, now).kind).toBe('none');
    });

    it('returns expired when past end date', () => {
        const result = resolveTenantLicenseLabel('2026-01-01T00:00:00Z', 'KEY', now);
        expect(result.kind).toBe('expired');
        expect(result.label).toBe('Abgelaufen');
    });

    it('returns trial for short window without key', () => {
        const result = resolveTenantLicenseLabel('2026-06-01T00:00:00Z', null, now);
        expect(result.kind).toBe('trial');
        expect(result.daysRemaining).toBeGreaterThan(0);
    });
});
