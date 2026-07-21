import { describe, expect, it } from 'vitest';

import { limitChartPoints } from '@/lib/charts/limitChartPoints';

describe('limitChartPoints', () => {
  it('returns the same array reference-equivalent content when under the cap', () => {
    const data = [{ n: 1 }, { n: 2 }, { n: 3 }];
    expect(limitChartPoints(data, 10)).toEqual(data);
  });

  it('keeps first and last points while thinning the middle', () => {
    const data = Array.from({ length: 200 }, (_, i) => ({ i }));
    const limited = limitChartPoints(data, 20);
    expect(limited.length).toBeLessThanOrEqual(20);
    expect(limited[0]).toEqual({ i: 0 });
    expect(limited[limited.length - 1]).toEqual({ i: 199 });
  });
});
