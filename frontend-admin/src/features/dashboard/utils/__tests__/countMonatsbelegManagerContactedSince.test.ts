import { describe, expect, it } from 'vitest';

import {
  countMonatsbelegManagerContactedSince,
  MONATSBELEG_ACTIVITY_LIST_LIMIT,
} from '@/features/dashboard/utils/countMonatsbelegManagerContactedSince';

describe('countMonatsbelegManagerContactedSince', () => {
  const since = new Date('2026-09-15T00:00:00.000Z');

  it('counts matching events in the last 7 days', () => {
    const count = countMonatsbelegManagerContactedSince(
      [
        { type: 'MonatsbelegManagerContacted', createdAtUtc: '2026-09-16T12:00:00.000Z' },
        { type: 'MonatsbelegManagerContacted', createdAtUtc: '2026-09-20T08:00:00.000Z' },
        { type: 'MonatsbelegCreated', createdAtUtc: '2026-09-20T08:00:00.000Z' },
        { type: 'MonatsbelegManagerContacted', createdAtUtc: '2026-09-10T08:00:00.000Z' },
      ],
      since
    );
    expect(count).toBe(2);
  });

  it('returns 0 for empty, malformed, or non-matching items', () => {
    expect(countMonatsbelegManagerContactedSince(null, since)).toBe(0);
    expect(countMonatsbelegManagerContactedSince([], since)).toBe(0);
    expect(
      countMonatsbelegManagerContactedSince(
        [{ type: 'MonatsbelegManagerContacted', createdAtUtc: 'not-a-date' }],
        since
      )
    ).toBe(0);
  });

  it('documents the activities list ceiling', () => {
    expect(MONATSBELEG_ACTIVITY_LIST_LIMIT).toBe(50);
  });
});
