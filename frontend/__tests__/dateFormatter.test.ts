import { describe, expect, it } from '@jest/globals';

import { formatUserDate, formatUserDateTime } from '../utils/dateFormatter';

describe('dateFormatter', () => {
  it('formats local date as DD.MM.YYYY', () => {
    const local = new Date(2025, 11, 1, 14, 30, 0);
    expect(formatUserDate(local)).toBe('01.12.2025');
  });

  it('formats datetime with seconds', () => {
    const local = new Date(2025, 11, 1, 14, 30, 45);
    expect(formatUserDateTime(local, { includeSeconds: true })).toBe('01.12.2025 14:30:45');
  });
});
