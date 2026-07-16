import { describe, expect, it } from 'vitest';

import type { CurrentTenant } from '@/features/tenancy/hooks/useCurrentTenantState';
import { resolveEffectiveTenant } from '@/features/tenancy/providers/TenantProvider';

function baseCurrentTenant(overrides: Partial<CurrentTenant> = {}): CurrentTenant {
    return {
        tenantSlug: 'dev',
        tenantId: 'dev-id',
        tenantName: 'Development',
        tenantStatus: 'Active',
        isActive: true,
        isTenantSuspended: false,
        licenseValidUntilUtc: '2026-12-31T00:00:00Z',
        licenseKey: null,
        licenseDaysRemaining: 100,
        resolvedTenant: null,
        displayLabel: 'Development',
        hasAuthToken: true,
        isImpersonating: false,
        isDevTenantOverride: true,
        isPlatformAdminHost: false,
        hostSlug: 'admin',
        requiresTenantSelection: false,
        isSuperAdminPlatformMode: false,
        isSuperAdminUser: false,
        isRealTenantSlug: true,
        showTenantLicenseInHeader: true,
        suppressLicenseWarnings: false,
        isTenantRecordLoading: false,
        ...overrides,
    };
}

describe('resolveEffectiveTenant', () => {
    it('prefers dev switcher identity over mismatched API snapshot', () => {
        const result = resolveEffectiveTenant(null, {
            id: 'default-id',
            slug: 'default',
            name: 'Default',
            licenseValid: true,
            licenseValidUntilUtc: '2026-01-01T00:00:00Z',
        }, baseCurrentTenant());

        expect(result).toEqual({
            id: 'dev-id',
            slug: 'dev',
            name: 'Development',
            licenseValid: true,
            licenseValidUntilUtc: '2026-12-31T00:00:00Z',
        });
    });

    it('uses API license fields when API tenant matches current tenant', () => {
        const result = resolveEffectiveTenant(null, {
            id: 'dev-id',
            slug: 'dev',
            name: 'Development',
            licenseValid: false,
            licenseValidUntilUtc: null,
        }, baseCurrentTenant());

        expect(result?.licenseValid).toBe(false);
        expect(result?.licenseValidUntilUtc).toBeNull();
    });
});
