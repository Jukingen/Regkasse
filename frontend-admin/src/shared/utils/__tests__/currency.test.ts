import { describe, expect, it } from 'vitest';

import { formatEUR } from '@/shared/utils/currency';

describe('formatEUR', () => {
  it('formats EUR with de-AT locale conventions', () => {
    const formatted = formatEUR(12.5);
    expect(formatted).toMatch(/12/);
    expect(formatted).toMatch(/€|EUR/);
  });

  it('handles zero', () => {
    expect(formatEUR(0)).toMatch(/0/);
  });
});
