/// <reference types="jest" />

import {
  formatLicenseRemainingDe,
  getLicenseHoursRemaining,
  normalizeLicenseDaysRemaining,
  preferLicenseHoursRemaining,
} from '../utils/licenseExpiryRemaining';

describe('licenseExpiryRemaining', () => {
  const nowMs = new Date('2026-07-16T19:15:00.000Z').getTime();

  it('returns ceil hours until expiry', () => {
    const expiresAt = new Date(nowMs + 4.2 * 60 * 60 * 1000).toISOString();
    expect(getLicenseHoursRemaining(expiresAt, nowMs)).toBe(5);
  });

  it('prefers hours when less than 24h remain', () => {
    const expiresAt = new Date(nowMs + 19 * 60 * 60 * 1000).toISOString();
    expect(preferLicenseHoursRemaining(1, expiresAt, nowMs)).toEqual({
      kind: 'hours',
      hours: 19,
    });
    expect(formatLicenseRemainingDe(1, expiresAt, nowMs)).toBe('19 Std.');
  });

  it('keeps days when more than 24h remain', () => {
    const expiresAt = new Date(nowMs + 36 * 60 * 60 * 1000).toISOString();
    expect(preferLicenseHoursRemaining(2, expiresAt, nowMs)).toEqual({
      kind: 'days',
      days: 2,
    });
    expect(formatLicenseRemainingDe(2, expiresAt, nowMs)).toBe('2 Tage');
    expect(formatLicenseRemainingDe(1, expiresAt, nowMs)).toBe('1 Tag');
  });

  it('truncates day counts without flooring negatives away incorrectly', () => {
    expect(normalizeLicenseDaysRemaining(1.9)).toBe(1);
    expect(normalizeLicenseDaysRemaining(-5.2)).toBe(-5);
    expect(normalizeLicenseDaysRemaining(null)).toBe(0);
  });
});
