import { describe, expect, it } from 'vitest';

import { limitUsagePercent, limitUsageTone } from '@/features/dev-limits/utils/limitUsage';

describe('limitUsage', () => {
  it('computes percent and clamps invalid limits', () => {
    expect(limitUsagePercent(8, 10)).toBe(80);
    expect(limitUsagePercent(0, 5)).toBe(0);
    expect(limitUsagePercent(3, 0)).toBe(0);
  });

  it('maps percent to warning tones', () => {
    expect(limitUsageTone(50)).toBe('ok');
    expect(limitUsageTone(80)).toBe('warning');
    expect(limitUsageTone(100)).toBe('exceeded');
  });
});
