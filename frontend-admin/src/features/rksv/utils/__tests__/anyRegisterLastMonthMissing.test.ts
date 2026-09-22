import { describe, expect, it } from 'vitest';

import { anyRegisterLastMonthMissing } from '@/features/rksv/utils/anyRegisterLastMonthMissing';

describe('anyRegisterLastMonthMissing', () => {
  it('is true when any register reports lastMonthMissing', () => {
    expect(
      anyRegisterLastMonthMissing([
        { status: { lastMonthMissing: false } },
        { status: { lastMonthMissing: true } },
      ])
    ).toBe(true);
  });

  it('is false when none are missing', () => {
    expect(anyRegisterLastMonthMissing([{ status: { lastMonthMissing: false } }])).toBe(false);
    expect(anyRegisterLastMonthMissing([])).toBe(false);
    expect(anyRegisterLastMonthMissing(null)).toBe(false);
  });
});
