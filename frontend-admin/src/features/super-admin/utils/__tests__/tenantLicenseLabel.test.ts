import { describe, expect, it } from 'vitest';

import { resolveTenantLicenseLabel } from '../tenantLicenseLabel';

describe('resolveTenantLicenseLabel', () => {
    const now = new Date('2026-05-20T12:00:00Z').getTime();

    it('returns none when license end date and key are missing', () => {
        const result = resolveTenantLicenseLabel(null, null, now);
        expect(result.kind).toBe('none');
        expect(result.daysRemaining).toBeNull();
    });

    it('returns valid with placeholder when key exists but end date is missing', () => {
        const result = resolveTenantLicenseLabel(null, 'REGK-TEST-KEY', now);
        expect(result.kind).toBe('valid');
        expect(result.label).toBe('—');
        expect(result.daysRemaining).toBeNull();
    });

    it('returns expired when end date is in the past', () => {
        const result = resolveTenantLicenseLabel('2026-01-01T00:00:00Z', 'KEY', now);
        expect(result.kind).toBe('expired');
        expect(result.label).toBe('Abgelaufen');
        expect(result.daysRemaining).toBeLessThan(0);
    });

    it('returns trial for short window without license key', () => {
        const result = resolveTenantLicenseLabel('2026-06-01T00:00:00Z', null, now);
        expect(result.kind).toBe('trial');
        expect(result.daysRemaining).toBeGreaterThan(0);
        expect(result.daysRemaining).toBeLessThanOrEqual(31);
    });

    it('returns trial when seven or fewer days remain (warning window)', () => {
        const result = resolveTenantLicenseLabel('2026-05-27T12:00:00Z', 'KEY', now);
        expect(result.kind).toBe('trial');
        expect(result.daysRemaining).toBeLessThanOrEqual(7);
        expect(result.daysRemaining).toBeGreaterThan(0);
    });

    it('returns valid when more than seven days remain with license key', () => {
        const result = resolveTenantLicenseLabel('2026-08-31T00:00:00Z', 'KEY', now);
        expect(result.kind).toBe('valid');
        expect(result.daysRemaining).toBeGreaterThan(7);
    });

    it('prefers server-computed days remaining when provided', () => {
        const result = resolveTenantLicenseLabel('2026-08-31T00:00:00Z', 'KEY', now, 42);
        expect(result.daysRemaining).toBe(42);
        expect(result.kind).toBe('valid');
    });

    it('uses server days remaining for expired classification', () => {
        const result = resolveTenantLicenseLabel('2026-08-31T00:00:00Z', 'KEY', now, -3);
        expect(result.kind).toBe('expired');
        expect(result.daysRemaining).toBe(-3);
    });

    it('returns valid when server days remain without end date (dev bypass)', () => {
        const result = resolveTenantLicenseLabel(null, 'REGK-KEY', now, 999);
        expect(result.kind).toBe('valid');
        expect(result.daysRemaining).toBe(999);
    });
});
