import { describe, expect, it } from '@jest/globals';

import {
  isLicenseLockoutSnapshotFresh,
  parseLicenseLockoutSnapshot,
} from '../utils/licenseLockoutSnapshot';

describe('licenseLockoutSnapshot', () => {
  it('parses valid snapshots and rejects corrupt payloads', () => {
    expect(parseLicenseLockoutSnapshot(null)).toBeNull();
    expect(parseLicenseLockoutSnapshot({ daysOverdue: 3 })).toBeNull();
    expect(parseLicenseLockoutSnapshot({ daysOverdue: 9, savedAtMs: 1_700_000_000_000 })).toEqual({
      daysOverdue: 9,
      savedAtMs: 1_700_000_000_000,
    });
    expect(
      parseLicenseLockoutSnapshot({ daysOverdue: -2.7, savedAtMs: 1_700_000_000_000 })?.daysOverdue
    ).toBe(0);
  });

  it('treats snapshots older than 24h as stale', () => {
    const now = 1_700_000_000_000;
    expect(
      isLicenseLockoutSnapshotFresh({ daysOverdue: 1, savedAtMs: now - 23 * 60 * 60 * 1000 }, now)
    ).toBe(true);
    expect(
      isLicenseLockoutSnapshotFresh({ daysOverdue: 1, savedAtMs: now - 25 * 60 * 60 * 1000 }, now)
    ).toBe(false);
  });
});
