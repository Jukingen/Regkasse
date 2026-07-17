import { describe, expect, it } from '@jest/globals';

import {
  computePosTagesabschlussClosingRequired,
  computeViennaHoursRemainingUntilMidnight,
} from '../utils/viennaTagesabschlussReminder';

describe('computePosTagesabschlussClosingRequired', () => {
  it('is true only when canClose is true', () => {
    expect(computePosTagesabschlussClosingRequired({ canClose: true })).toBe(true);
    expect(computePosTagesabschlussClosingRequired({ canClose: false })).toBe(false);
  });
});

describe('computeViennaHoursRemainingUntilMidnight', () => {
  it('returns a value between 0 and 24 inclusive', () => {
    const hours = computeViennaHoursRemainingUntilMidnight(new Date());
    expect(hours).toBeGreaterThanOrEqual(0);
    expect(hours).toBeLessThanOrEqual(24);
  });

  it('returns 1 when less than one hour remains before Vienna midnight', () => {
    // Fixed UTC instant: 2026-07-16 21:45 UTC = 23:45 Vienna (CEST, UTC+2)
    const nearMidnight = new Date('2026-07-16T21:45:00.000Z');
    expect(computeViennaHoursRemainingUntilMidnight(nearMidnight)).toBe(1);
  });

  it('returns about 12 hours around Vienna noon', () => {
    // 2026-07-16 10:00 UTC = 12:00 Vienna (CEST)
    const noon = new Date('2026-07-16T10:00:00.000Z');
    expect(computeViennaHoursRemainingUntilMidnight(noon)).toBe(12);
  });
});
