import { describe, expect, it } from 'vitest';

import {
  getLicenseCountdownAccentColor,
  getLicenseCountdownProgressPercent,
} from '@/features/license/utils/licenseCountdownWidget';

describe('licenseCountdownWidget', () => {
  it('maps remaining days to accent colors', () => {
    expect(getLicenseCountdownAccentColor(true, 0)).toBe('#cf1322');
    expect(getLicenseCountdownAccentColor(false, 3)).toBe('#faad14');
    expect(getLicenseCountdownAccentColor(false, 14)).toBe('#1890ff');
    expect(getLicenseCountdownAccentColor(false, 90)).toBe('#52c41a');
  });

  it('clamps progress to a 365-day year', () => {
    expect(getLicenseCountdownProgressPercent(true, 0)).toBe(0);
    expect(getLicenseCountdownProgressPercent(false, 365)).toBe(100);
    expect(getLicenseCountdownProgressPercent(false, 73)).toBe(20);
  });
});
