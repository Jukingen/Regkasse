import { describe, expect, it } from 'vitest';

import type { TenantLicenseOverview } from '@/features/license/api/tenantLicense';
import { mapTenantLicenseOverviewToPublicStatus } from '@/features/license/utils/mapTenantLicenseOverviewToPublicStatus';

describe('mapTenantLicenseOverviewToPublicStatus', () => {
  it('maps active tenant overview to public status', () => {
    const overview: TenantLicenseOverview = {
      status: {
        kind: 'active',
        licenseKey: 'TEST-abc',
        validUntilUtc: '2026-07-16T00:00:00.000Z',
        daysRemaining: 1,
        tier: 'Premium',
        features: ['pos'],
      },
      history: [],
    };

    const mapped = mapTenantLicenseOverviewToPublicStatus(overview);

    expect(mapped.daysRemaining).toBe(1);
    expect(mapped.validUntil).toBe('2026-07-16T00:00:00.000Z');
    expect(mapped.canAccess).toBe(true);
    expect(mapped.isValid).toBe(true);
    expect(mapped.isExpired).toBe(false);
  });

  it('maps lockdown tenant overview to expired public status', () => {
    const overview: TenantLicenseOverview = {
      status: {
        kind: 'lockdown',
        licenseKey: 'TEST-expired',
        validUntilUtc: '2026-01-01T00:00:00.000Z',
        daysRemaining: -30,
        features: [],
      },
      history: [],
    };

    const mapped = mapTenantLicenseOverviewToPublicStatus(overview);

    expect(mapped.canAccess).toBe(false);
    expect(mapped.isExpired).toBe(true);
    expect(mapped.requiresRenewal).toBe(true);
  });
});
