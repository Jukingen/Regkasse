import { describe, expect, it } from 'vitest';

import {
  LICENSE_DAYS_LONG_TERM_THRESHOLD,
  LICENSE_DAYS_UNLIMITED_THRESHOLD,
  computeLicenseDaysRemaining,
  formatLicenseDaysRemainingLabel,
  formatLicenseValidityTooltip,
  getLicenseDaysRemainingSortValue,
  isLicenseDaysUnlimited,
  licenseValidityHealthTagColor,
  resolveLicenseValidityHealth,
} from '@/features/billing/utils/billingFormatters';

const t = (key: string, options?: Record<string, string | number>) => {
  const days = options?.days;
  switch (key) {
    case 'billing.licenseSales.unlimited':
      return 'Unlimited';
    case 'billing.licenseSales.endsToday':
      return 'Ends today';
    case 'billing.licenseSales.overdue':
      return `${days} days overdue`;
    case 'billing.licenseSales.daysRemainingFormatOne':
      return `${days} day`;
    case 'billing.licenseSales.daysRemainingFormat':
      return `${days} days`;
    case 'billing.licenseSales.healthTooltip.expiresIn':
      return `This license expires in ${days} days`;
    case 'billing.licenseSales.healthTooltip.expiresInOne':
      return `This license expires in ${days} day`;
    case 'billing.licenseSales.healthTooltip.expiresToday':
      return 'This license expires today';
    case 'billing.licenseSales.healthTooltip.expired':
      return `This license expired ${days} days ago`;
    case 'billing.licenseSales.healthTooltip.longTerm':
      return `Long-term license (valid for more than ${options?.years ?? 2} years)`;
    default:
      return key;
  }
};

describe('computeLicenseDaysRemaining', () => {
  it('returns null for missing or invalid dates', () => {
    expect(computeLicenseDaysRemaining(null)).toBeNull();
    expect(computeLicenseDaysRemaining(undefined)).toBeNull();
    expect(computeLicenseDaysRemaining('not-a-date')).toBeNull();
  });

  it('computes calendar-day remaining / overdue / today', () => {
    const now = new Date('2026-08-11T15:30:00.000Z');
    expect(computeLicenseDaysRemaining('2026-08-11T23:59:59.000Z', now)).toBe(0);
    expect(computeLicenseDaysRemaining('2026-08-12T00:00:00.000Z', now)).toBe(1);
    expect(computeLicenseDaysRemaining('2026-08-21T12:00:00.000Z', now)).toBe(10);
    expect(computeLicenseDaysRemaining('2026-08-01T00:00:00.000Z', now)).toBe(-10);
  });
});

describe('isLicenseDaysUnlimited / sort / label', () => {
  it('treats > 5 years as unlimited', () => {
    expect(isLicenseDaysUnlimited(LICENSE_DAYS_UNLIMITED_THRESHOLD)).toBe(false);
    expect(isLicenseDaysUnlimited(LICENSE_DAYS_UNLIMITED_THRESHOLD + 1)).toBe(true);
  });

  it('sorts overdue before remaining before unlimited', () => {
    const now = new Date('2026-08-11T12:00:00.000Z');
    const overdue = getLicenseDaysRemainingSortValue('2026-08-01T00:00:00.000Z', now);
    const soon = getLicenseDaysRemainingSortValue('2026-08-21T00:00:00.000Z', now);
    const unlimited = getLicenseDaysRemainingSortValue('2040-01-01T00:00:00.000Z', now);
    expect(overdue).toBeLessThan(soon);
    expect(soon).toBeLessThan(unlimited);
    expect(unlimited).toBe(Number.POSITIVE_INFINITY);
  });

  it('formats labels for remaining, today, overdue, and unlimited', () => {
    const now = new Date('2026-08-11T12:00:00.000Z');
    expect(formatLicenseDaysRemainingLabel('2026-08-12T00:00:00.000Z', t, now)).toBe('1 day');
    expect(formatLicenseDaysRemainingLabel('2026-08-21T00:00:00.000Z', t, now)).toBe('10 days');
    expect(formatLicenseDaysRemainingLabel('2026-08-11T00:00:00.000Z', t, now)).toBe('Ends today');
    expect(formatLicenseDaysRemainingLabel('2026-08-01T00:00:00.000Z', t, now)).toBe(
      '10 days overdue'
    );
    expect(formatLicenseDaysRemainingLabel('2040-01-01T00:00:00.000Z', t, now)).toBe('Unlimited');
    expect(formatLicenseDaysRemainingLabel(null, t, now)).toBe('—');
  });
});

describe('resolveLicenseValidityHealth / tooltip', () => {
  const now = new Date('2026-08-11T12:00:00.000Z');

  it('maps day bands to health colors', () => {
    expect(resolveLicenseValidityHealth('2026-07-01T00:00:00.000Z', now)).toBe('expired');
    expect(resolveLicenseValidityHealth('2026-08-11T00:00:00.000Z', now)).toBe('critical');
    expect(resolveLicenseValidityHealth('2026-08-18T00:00:00.000Z', now)).toBe('critical'); // 7d
    expect(resolveLicenseValidityHealth('2026-08-19T00:00:00.000Z', now)).toBe('warning'); // 8d
    expect(resolveLicenseValidityHealth('2026-09-10T00:00:00.000Z', now)).toBe('warning'); // 30d
    expect(resolveLicenseValidityHealth('2026-09-11T00:00:00.000Z', now)).toBe('healthy'); // 31d
    expect(
      resolveLicenseValidityHealth(
        new Date(now.getTime() + (LICENSE_DAYS_LONG_TERM_THRESHOLD + 1) * 86400000).toISOString(),
        now
      )
    ).toBe('longTerm');

    expect(licenseValidityHealthTagColor('healthy')).toBe('success');
    expect(licenseValidityHealthTagColor('warning')).toBe('gold');
    expect(licenseValidityHealthTagColor('critical')).toBe('orange');
    expect(licenseValidityHealthTagColor('expired')).toBe('error');
    expect(licenseValidityHealthTagColor('longTerm')).toBe('default');
  });

  it('builds health tooltips', () => {
    expect(formatLicenseValidityTooltip('2026-08-21T00:00:00.000Z', t, now)).toBe(
      'This license expires in 10 days'
    );
    expect(formatLicenseValidityTooltip('2026-08-12T00:00:00.000Z', t, now)).toBe(
      'This license expires in 1 day'
    );
    expect(formatLicenseValidityTooltip('2026-08-11T00:00:00.000Z', t, now)).toBe(
      'This license expires today'
    );
    expect(formatLicenseValidityTooltip('2026-08-01T00:00:00.000Z', t, now)).toBe(
      'This license expired 10 days ago'
    );
    expect(formatLicenseValidityTooltip('2040-01-01T00:00:00.000Z', t, now)).toBe(
      'Long-term license (valid for more than 2 years)'
    );
  });
});
