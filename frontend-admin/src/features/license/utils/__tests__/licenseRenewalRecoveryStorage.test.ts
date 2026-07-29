import { afterEach, describe, expect, it, vi } from 'vitest';

import {
  LICENSE_RENEWAL_RECOVERY_TTL_MS,
  clearLicenseRenewalPending,
  isLicenseRenewalPending,
  markLicenseRenewalPending,
} from '../licenseRenewalRecoveryStorage';

describe('licenseRenewalRecoveryStorage', () => {
  afterEach(() => {
    localStorage.clear();
    vi.useRealTimers();
  });

  it('marks and detects pending within TTL', () => {
    const now = Date.parse('2026-07-27T12:00:00.000Z');
    vi.setSystemTime(now);
    markLicenseRenewalPending('tenant-a', new Date(now).toISOString());
    expect(isLicenseRenewalPending('tenant-a', now)).toBe(true);
    expect(localStorage.getItem('licenseRenewalPending')).toBe('true');
  });

  it('expires after one hour and clears keys', () => {
    const started = Date.parse('2026-07-27T12:00:00.000Z');
    markLicenseRenewalPending('tenant-a', new Date(started).toISOString());
    expect(
      isLicenseRenewalPending('tenant-a', started + LICENSE_RENEWAL_RECOVERY_TTL_MS)
    ).toBe(false);
    expect(localStorage.getItem('regkasse.license.renewalPending.tenant-a')).toBeNull();
  });

  it('clear removes tenant and legacy keys', () => {
    markLicenseRenewalPending('tenant-a');
    clearLicenseRenewalPending('tenant-a');
    expect(isLicenseRenewalPending('tenant-a')).toBe(false);
    expect(localStorage.getItem('licenseRenewalPending')).toBeNull();
  });
});
