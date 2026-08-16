import { describe, expect, it } from 'vitest';

import {
  calculateLicenseDaysRemaining,
  calculateLicenseDaysRemainingUnsigned,
  formatLicenseValidUntil,
  licenseValidUntilHasTime,
  parseLicenseValidUntilMs,
} from '../licenseValidUntil';

describe('licenseValidUntil', () => {
  const nowMs = new Date('2026-08-14T12:00:00.000Z').getTime();

  it('parses ISO validUntil timestamps', () => {
    expect(parseLicenseValidUntilMs('2026-08-14T23:59:59.000Z')).toBe(
      Date.parse('2026-08-14T23:59:59.000Z')
    );
    expect(parseLicenseValidUntilMs('')).toBeNull();
    expect(parseLicenseValidUntilMs('not-a-date')).toBeNull();
  });

  it('calculates remaining days as ceil((validUntil - now) / 1 day)', () => {
    expect(calculateLicenseDaysRemaining('2026-08-24T12:00:00.000Z', nowMs)).toBe(10);
    expect(calculateLicenseDaysRemaining('2026-08-14T18:00:00.000Z', nowMs)).toBe(1);
    expect(calculateLicenseDaysRemaining('2026-08-14T12:00:00.000Z', nowMs)).toBe(0);
    expect(calculateLicenseDaysRemaining('2026-08-04T12:00:00.000Z', nowMs)).toBe(-10);
    expect(calculateLicenseDaysRemaining(null, nowMs)).toBeNull();
  });

  it('clamps unsigned remaining days at zero for active cache patches', () => {
    expect(calculateLicenseDaysRemainingUnsigned('2026-08-24T12:00:00.000Z', nowMs)).toBe(10);
    expect(calculateLicenseDaysRemainingUnsigned('2026-08-04T12:00:00.000Z', nowMs)).toBe(0);
  });

  it('formats midnight UTC as DD.MM.YYYY without time', () => {
    expect(formatLicenseValidUntil('2026-08-14T00:00:00.000Z')).toBe('14.08.2026');
    expect(licenseValidUntilHasTime('2026-08-14T00:00:00.000Z')).toBe(false);
  });

  it('shows time when the UTC stamp is not midnight', () => {
    expect(formatLicenseValidUntil('2026-07-20T21:30:00.000Z')).toBe('20.07.2026 21:30');
    expect(licenseValidUntilHasTime('2026-07-20T21:30:00.000Z')).toBe(true);
  });

  it('keeps the UTC calendar day for end-of-day stamps (no local TZ shift)', () => {
    expect(formatLicenseValidUntil('2026-08-14T23:59:59.000Z')).toBe('14.08.2026 23:59');
    expect(formatLicenseValidUntil('2026-08-14T23:59:59.000Z', 'date')).toBe('14.08.2026');
  });

  it('returns em dash for missing or invalid dates', () => {
    expect(formatLicenseValidUntil(null)).toBe('—');
    expect(formatLicenseValidUntil('bogus')).toBe('—');
  });
});
