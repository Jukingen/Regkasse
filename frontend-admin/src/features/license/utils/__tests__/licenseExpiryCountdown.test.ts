import { describe, expect, it } from 'vitest';

import {
  formatLicenseExpiryCountdown,
  getLicenseExpiryCountdownParts,
} from '../licenseExpiryCountdown';

describe('licenseExpiryCountdown', () => {
  const nowMs = Date.parse('2026-07-15T10:00:00.000Z');

  it('formats remaining time as days, hours, and minutes', () => {
    const expiresAt = '2026-07-20T22:30:00.000Z';
    expect(formatLicenseExpiryCountdown(expiresAt, nowMs)).toBe('5d 12h 30m');
  });

  it('returns null when expiry is in the past', () => {
    expect(formatLicenseExpiryCountdown('2026-07-01T00:00:00.000Z', nowMs)).toBeNull();
  });

  it('returns null when expiry is unknown', () => {
    expect(formatLicenseExpiryCountdown(null, nowMs)).toBeNull();
    expect(getLicenseExpiryCountdownParts(undefined, nowMs)).toBeNull();
  });

  it('returns zero parts when already expired', () => {
    expect(getLicenseExpiryCountdownParts('2026-07-01T00:00:00.000Z', nowMs)).toEqual({
      days: 0,
      hours: 0,
      minutes: 0,
      totalMs: expect.any(Number),
    });
  });
});
