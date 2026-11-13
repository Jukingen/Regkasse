import { describe, expect, it } from 'vitest';

import {
  getGracePeriodConsumedPercent,
  getGracePeriodProgressPercent,
} from '@/features/license/utils/gracePeriodProgress';

describe('getGracePeriodProgressPercent', () => {
  it('maps remaining days onto the grace window', () => {
    expect(getGracePeriodProgressPercent(7, 7)).toBe(100);
    expect(getGracePeriodProgressPercent(3.5, 7)).toBe(50);
    expect(getGracePeriodProgressPercent(0, 7)).toBe(0);
  });

  it('clamps invalid inputs', () => {
    expect(getGracePeriodProgressPercent(20, 7)).toBe(100);
    expect(getGracePeriodProgressPercent(-1, 7)).toBe(0);
    expect(getGracePeriodProgressPercent(3, 0)).toBe(0);
  });
});

describe('getGracePeriodConsumedPercent', () => {
  it('fills as grace days are consumed', () => {
    expect(getGracePeriodConsumedPercent(7, 7)).toBe(0);
    expect(getGracePeriodConsumedPercent(3.5, 7)).toBe(50);
    expect(getGracePeriodConsumedPercent(0, 7)).toBe(100);
  });

  it('clamps invalid inputs', () => {
    expect(getGracePeriodConsumedPercent(20, 7)).toBe(0);
    expect(getGracePeriodConsumedPercent(-1, 7)).toBe(100);
    expect(getGracePeriodConsumedPercent(3, 0)).toBe(0);
  });
});
