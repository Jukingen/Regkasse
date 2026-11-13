import { describe, expect, it } from 'vitest';

import {
  getLicenseHealthPercent,
  getLicenseHealthStrokeColor,
} from '@/features/license/utils/licenseHealthWidget';

describe('getLicenseHealthPercent', () => {
  it('maps Active days onto a 365-day year', () => {
    expect(
      getLicenseHealthPercent({
        state: 'Active',
        daysUntilExpiry: 365,
        graceDaysRemaining: 0,
      })
    ).toBe(100);

    expect(
      getLicenseHealthPercent({
        state: 'Active',
        daysUntilExpiry: 182.5,
        graceDaysRemaining: 0,
      })
    ).toBe(50);
  });

  it('uses grace remaining over the grace window', () => {
    expect(
      getLicenseHealthPercent({
        state: 'Grace',
        daysUntilExpiry: 0,
        graceDaysRemaining: 7,
      })
    ).toBe(100);

    expect(
      getLicenseHealthPercent({
        state: 'Grace',
        daysUntilExpiry: 0,
        graceDaysRemaining: 3.5,
      })
    ).toBe(50);
  });

  it('returns 0 for Locked/Archived or missing expiry', () => {
    expect(
      getLicenseHealthPercent({
        state: 'Locked',
        daysUntilExpiry: 0,
        graceDaysRemaining: 0,
      })
    ).toBe(0);

    expect(
      getLicenseHealthPercent({
        state: 'Archived',
        daysUntilExpiry: 0,
        graceDaysRemaining: 0,
      })
    ).toBe(0);

    expect(
      getLicenseHealthPercent({
        state: 'Active',
        daysUntilExpiry: 100,
        graceDaysRemaining: 0,
        hasExpiry: false,
      })
    ).toBe(0);
  });
});

describe('getLicenseHealthStrokeColor', () => {
  it('returns lifecycle colors', () => {
    expect(getLicenseHealthStrokeColor('Active', 40)).toBe('#52c41a');
    expect(getLicenseHealthStrokeColor('Active', 20)).toBe('#1890ff');
    expect(getLicenseHealthStrokeColor('Active', 5)).toBe('#faad14');
    expect(getLicenseHealthStrokeColor('Grace', 3)).toBe('#faad14');
    expect(getLicenseHealthStrokeColor('Locked', 0)).toBe('#cf1322');
    expect(getLicenseHealthStrokeColor('Archived', 0)).toBe('#cf1322');
  });
});
