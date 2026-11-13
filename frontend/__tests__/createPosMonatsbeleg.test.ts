import { describe, expect, it } from '@jest/globals';

import {
  getViennaYearMonth,
  resolvePosMonatsbelegTarget,
} from '../utils/resolvePosMonatsbelegTarget';

describe('resolvePosMonatsbelegTarget', () => {
  it('prefers earliest overdue missing month', () => {
    expect(
      resolvePosMonatsbelegTarget({
        nextRequiredMonth: '2026-05',
        missingMonths: [
          { year: 2026, month: 5, isOverdue: false },
          { year: 2026, month: 4, isOverdue: true },
        ],
      })
    ).toEqual({ year: 2026, month: 4 });
  });

  it('uses first missing month when none overdue', () => {
    expect(
      resolvePosMonatsbelegTarget({
        nextRequiredMonth: '2026-06',
        missingMonths: [{ year: 2026, month: 6, isOverdue: false }],
      })
    ).toEqual({ year: 2026, month: 6 });
  });

  it('parses nextRequiredMonth when missingMonths empty', () => {
    expect(
      resolvePosMonatsbelegTarget({
        nextRequiredMonth: '2025-12',
        missingMonths: [],
      })
    ).toEqual({ year: 2025, month: 12 });
  });

  it('falls back to previous Vienna month', () => {
    const vienna = getViennaYearMonth();
    const previous =
      vienna.month <= 1
        ? { year: vienna.year - 1, month: 12 }
        : { year: vienna.year, month: vienna.month - 1 };
    expect(resolvePosMonatsbelegTarget(null)).toEqual(previous);
    expect(resolvePosMonatsbelegTarget(undefined)).toEqual(previous);
  });
});
