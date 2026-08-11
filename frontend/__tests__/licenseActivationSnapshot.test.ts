import { licenseStatusFromActivationResult } from '../utils/licenseActivationSnapshot';
import type { LicenseStatus } from '../services/license/licenseStatusCache';

describe('licenseStatusFromActivationResult', () => {
  const prev: LicenseStatus = {
    isValid: false,
    isTrial: true,
    isExpired: true,
    daysRemaining: 0,
    expiryDate: '2020-01-01T00:00:00Z',
    machineHash: 'abc123',
    licenseType: 'Trial',
    mode: 'Trial',
  };

  it('maps activation payload onto a paid snapshot immediately', () => {
    const next = licenseStatusFromActivationResult(
      {
        success: true,
        validUntil: '2027-08-11T23:59:59Z',
        licenseType: 'Licensed',
        daysRemaining: 365,
        status: 'active',
      },
      prev
    );

    expect(next.isValid).toBe(true);
    expect(next.isTrial).toBe(false);
    expect(next.isExpired).toBe(false);
    expect(next.daysRemaining).toBe(365);
    expect(next.expiryDate).toBe('2027-08-11T23:59:59Z');
    expect(next.licenseType).toBe('Licensed');
    expect(next.mode).toBe('Production');
    expect(next.machineHash).toBe('abc123');
  });

  it('derives daysRemaining from validUntil when API omits it', () => {
    const inTenDays = new Date(Date.now() + 10 * 86_400_000).toISOString();
    const next = licenseStatusFromActivationResult(
      {
        success: true,
        validUntil: inTenDays,
        licenseType: 'Licensed',
      },
      null
    );

    expect(next.daysRemaining).toBeGreaterThanOrEqual(9);
    expect(next.daysRemaining).toBeLessThanOrEqual(11);
    expect(next.expiryDate).toBe(inTenDays);
  });
});
