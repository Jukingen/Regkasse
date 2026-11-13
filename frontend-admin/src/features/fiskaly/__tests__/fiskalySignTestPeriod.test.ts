import { describe, expect, it } from 'vitest';

import {
  previousViennaMonth,
  previousViennaYear,
  viennaNowParts,
} from '@/features/fiskaly/fiskalySignTestPeriod';

describe('fiskalySignTestPeriod', () => {
  it('computes previous Vienna month across year boundary', () => {
    const jan = new Date('2026-01-15T12:00:00.000Z');
    expect(viennaNowParts(jan)).toEqual({ year: 2026, month: 1 });
    expect(previousViennaMonth(jan)).toEqual({ year: 2025, month: 12 });
    expect(previousViennaYear(jan)).toBe(2025);
  });

  it('computes previous Vienna month inside the same year', () => {
    const aug = new Date('2026-08-30T10:00:00.000Z');
    expect(previousViennaMonth(aug)).toEqual({ year: 2026, month: 7 });
    expect(previousViennaYear(aug)).toBe(2025);
  });
});
