import { describe, expect, it } from 'vitest';

import {
    resolveDeploymentLicenseStatus,
    resolveTenantLicenseStatus,
    resolveTenantRowLicenseStatus,
} from '../licenseStatus';

describe('licenseStatus', () => {
    const nowMs = new Date('2026-05-20T12:00:00Z').getTime();

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
});
