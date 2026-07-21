import { describe, expect, it } from 'vitest';

import type { LicensePublicStatusDto } from '@/api/manual/adminLicense';
import { toTenantLicenseViewModel } from '@/hooks/useTenantLicense';

describe('toTenantLicenseViewModel', () => {
  it('adds German validUntilFormatted from ISO validUntil', () => {
    const status: LicensePublicStatusDto = {
      licenseType: 'Licensed',
      validUntil: '2026-07-16T14:30:00.000Z',
      daysRemaining: 10,
      features: [],
      isExpired: false,
      isValid: true,
    };

    const view = toTenantLicenseViewModel(status);

    expect(view.validUntil).toBe(status.validUntil);
    expect(view.validUntilFormatted).toMatch(/^\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}$/);
  });

  it('uses em dash when validUntil is missing', () => {
    const status: LicensePublicStatusDto = {
      licenseType: 'Trial',
      validUntil: null,
      daysRemaining: 999,
      features: [],
      isExpired: false,
      isValid: true,
    };

    expect(toTenantLicenseViewModel(status).validUntilFormatted).toBe('—');
  });
});
