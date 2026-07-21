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

  it('falls back to current Vienna month', () => {
    const vienna = getViennaYearMonth();
    expect(resolvePosMonatsbelegTarget(null)).toEqual(vienna);
    expect(resolvePosMonatsbelegTarget(undefined)).toEqual(vienna);
  });
});
