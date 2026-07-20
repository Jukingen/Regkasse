import { describe, expect, it } from 'vitest';

import {
    getLicenseStatusRemainingText,
    mapPublicStatusToTenantLicenseStatus,
    resolveDeploymentLicenseStatus,
    resolveTenantLicenseFromPublicStatus,
    resolveTenantLicenseStatus,
    resolveTenantRowLicenseStatus,
} from '../licenseStatus';

describe('licenseStatus', () => {
    const nowMs = new Date('2026-05-20T12:00:00Z').getTime();
    const t = (key: string, params?: Record<string, string | number>) => {
        if (key === 'license.phase.hoursRemaining') return `${params?.hours ?? 0} Stunden verbleibend`;
        if (key === 'license.phase.daysRemaining') return `${params?.days ?? 0} Tage verbleibend`;
        if (key === 'license.phase.daysExpired') return `Seit ${params?.days ?? 0} Tagen abgelaufen`;
        return key;
    };

    it('maps tenant grace_read_only to grace_readonly permissions', () => {
        const status = resolveTenantLicenseStatus(
            {
                kind: 'grace_read_only',
                licenseKey: 'REGK-KEY',
                validUntilUtc: '2026-04-05T00:00:00Z',
                daysRemaining: -45,
            },
            nowMs,
        );

        expect(status.kind).toBe('grace_readonly');
        expect(status.daysExpired).toBe(45);
        expect(status.canWrite).toBe(false);
        expect(status.canManageUsers).toBe(true);
        expect(status.canAccess).toBe(true);
    });

    it('derives tenant lockdown after grace when only validity is available', () => {
        const status = resolveTenantRowLicenseStatus(
            {
                licenseKey: 'REGK-KEY',
                licenseValidUntilUtc: '2026-04-25T00:00:00Z',
            },
            nowMs,
        );

        expect(status.kind).toBe('lockdown');
        expect(status.daysExpired).toBe(25);
        expect(status.canAccess).toBe(false);
    });

    it('derives tenant lockdown from row data when only validity is available', () => {
        const status = resolveTenantRowLicenseStatus(
            {
                licenseKey: 'REGK-KEY',
                licenseValidUntilUtc: '2026-01-15T00:00:00Z',
            },
            nowMs,
        );

        expect(status.kind).toBe('lockdown');
        expect(status.daysExpired).toBe(125);
        expect(status.canAccess).toBe(false);
    });

    it('maps deployment expired snapshot into grace_readonly phase', () => {
        const status = resolveDeploymentLicenseStatus(
            {
                isValid: false,
                isTrial: false,
                isExpired: true,
                daysRemaining: 0,
                expiryDate: '2026-04-25T00:00:00Z',
                machineHash: 'machine',
            },
            nowMs,
        );

        expect(status.kind).toBe('grace_readonly');
        expect(status.daysExpired).toBe(25);
        expect(status.canWrite).toBe(false);
        expect(status.canAccess).toBe(true);
    });

    it('treats active trial deployment snapshots as active', () => {
        const status = resolveDeploymentLicenseStatus(
            {
                isValid: false,
                isTrial: true,
                isExpired: false,
                daysRemaining: 12,
                expiryDate: '2026-06-01T00:00:00Z',
                machineHash: 'machine',
            },
            nowMs,
        );

        expect(status.kind).toBe('active');
        expect(status.daysRemaining).toBe(12);
        expect(status.canWrite).toBe(true);
        expect(status.canAccess).toBe(true);
    });

    it('maps dev bypass public status to active tenant license', () => {
        const status = resolveTenantLicenseFromPublicStatus({
            licenseType: 'Licensed',
            validUntil: null,
            daysRemaining: 999,
            features: [],
            isExpired: false,
            isValid: true,
            canAccess: true,
            canTransact: true,
            isDevelopmentBypass: true,
        });

        expect(status.kind).toBe('active');
        expect(status.daysRemaining).toBe(999);
        expect(status.canAccess).toBe(true);
    });

    it('maps mandant grace period from public status overlay', () => {
        const status = resolveTenantLicenseFromPublicStatus({
            licenseType: 'Licensed',
            validUntil: '2026-05-10T00:00:00Z',
            daysRemaining: -5,
            features: [],
            isExpired: false,
            isValid: true,
            canAccess: true,
            canTransact: true,
            isInGracePeriod: true,
            gracePeriodRemaining: 2,
        });

        expect(status.kind).toBe('grace_write');
        expect(status.canWrite).toBe(true);
        expect(status.daysExpired).toBe(5);
    });

    it('ignores positive ValidUntil horizon daysRemaining while in grace', () => {
        const status = resolveTenantLicenseFromPublicStatus(
            {
                licenseType: 'Licensed',
                validUntil: '2029-04-13T00:00:00Z',
                daysRemaining: 997,
                features: [],
                isExpired: true,
                isValid: true,
                canAccess: true,
                canTransact: true,
                isInGracePeriod: true,
                gracePeriodRemaining: 5,
            },
            nowMs,
        );

        expect(status.kind).toBe('grace_write');
        expect(status.daysRemaining).toBeLessThan(0);
        expect(status.daysExpired).toBe(2);
        expect(status.daysRemaining).not.toBe(997);
    });

    it('normalizes explicit grace_write with future ValidUntil horizon', () => {
        const status = resolveTenantLicenseStatus(
            {
                kind: 'grace_write',
                licenseKey: 'REGK-KEY',
                validUntilUtc: '2029-04-13T00:00:00Z',
                daysRemaining: 997,
            },
            nowMs,
        );

        expect(status.kind).toBe('grace_write');
        expect(status.daysRemaining).toBeLessThan(0);
        expect(status.daysRemaining).not.toBe(997);
    });

    it('shows overdue text for grace instead of ValidUntil remaining days', () => {
        const text = getLicenseStatusRemainingText(
            {
                kind: 'grace_write',
                daysRemaining: 997,
                daysExpired: 2,
                canWrite: true,
                canManageUsers: true,
                canAccess: true,
            },
            t,
            '2029-04-13T00:00:00Z',
            nowMs,
        );

        expect(text).toBe('Seit 2 Tagen abgelaufen');
        expect(text).not.toContain('997');
    });

    it('maps public status to tenant license display fields for Manager UI', () => {
        const status = mapPublicStatusToTenantLicenseStatus({
            licenseType: 'Licensed',
            validUntil: '2026-07-16T00:00:00Z',
            daysRemaining: 1,
            features: ['admin_basic'],
            isExpired: false,
            isValid: true,
            canAccess: true,
            canTransact: true,
        });

        expect(status.kind).toBe('active');
        expect(status.validUntilUtc).toBe('2026-07-16T00:00:00Z');
        expect(status.daysRemaining).toBe(1);
        expect(status.licenseKey).toBeNull();
        expect(status.features).toEqual(['admin_basic']);
    });

    it('prefers hours remaining text when less than 24h left', () => {
        const validUntil = new Date(nowMs + 5 * 60 * 60 * 1000).toISOString();
        const text = getLicenseStatusRemainingText(
            {
                kind: 'active',
                daysRemaining: 1,
                daysExpired: 0,
                canWrite: true,
                canManageUsers: true,
                canAccess: true,
            },
            t,
            validUntil,
            nowMs,
        );

        expect(text).toBe('5 Stunden verbleibend');
    });

    it('keeps day remaining text when more than 24h left', () => {
        const validUntil = new Date(nowMs + 36 * 60 * 60 * 1000).toISOString();
        const text = getLicenseStatusRemainingText(
            {
                kind: 'active',
                daysRemaining: 2,
                daysExpired: 0,
                canWrite: true,
                canManageUsers: true,
                canAccess: true,
            },
            t,
            validUntil,
            nowMs,
        );

        expect(text).toBe('2 Tage verbleibend');
    });
});
