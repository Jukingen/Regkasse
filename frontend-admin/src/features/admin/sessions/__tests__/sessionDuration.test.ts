import { describe, expect, it } from 'vitest';

import { formatSessionDuration } from '../sessionDuration';

describe('formatSessionDuration', () => {
  it('formats seconds, minutes, hours, and days', () => {
    expect(formatSessionDuration(12)).toBe('12s');
    expect(formatSessionDuration(90)).toBe('1m');
    expect(formatSessionDuration(3600)).toBe('1h 0m');
    expect(formatSessionDuration(90000)).toBe('1d 1h');
  });

  it('returns a dash for missing values', () => {
    expect(formatSessionDuration(null)).toBe('—');
    expect(formatSessionDuration(-1)).toBe('—');
  });
});
