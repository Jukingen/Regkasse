import { describe, expect, it } from 'vitest';

import {
  graceUrgentDismissStorageKey,
  isGracePeriodBannerUrgent,
  shouldShowGraceUrgentWarning,
} from '../gracePeriodUrgentWarning';
import { formatGraceLockCountdown } from '../graceLockCountdown';

describe('shouldShowGraceUrgentWarning', () => {
  it('shows only on final Grace day', () => {
    expect(shouldShowGraceUrgentWarning({ state: 'Grace', graceDaysRemaining: 1 })).toBe(true);
    expect(shouldShowGraceUrgentWarning({ state: 'Grace', graceDaysRemaining: 0 })).toBe(true);
    expect(shouldShowGraceUrgentWarning({ state: 'Grace', graceDaysRemaining: 2 })).toBe(false);
    expect(shouldShowGraceUrgentWarning({ state: 'Active', graceDaysRemaining: 0 })).toBe(false);
    expect(shouldShowGraceUrgentWarning(null)).toBe(false);
  });
});

describe('isGracePeriodBannerUrgent', () => {
  it('escalates banner when ≤2 grace days remain', () => {
    expect(isGracePeriodBannerUrgent({ state: 'Grace', graceDaysRemaining: 2 })).toBe(true);
    expect(isGracePeriodBannerUrgent({ state: 'Grace', graceDaysRemaining: 1 })).toBe(true);
    expect(isGracePeriodBannerUrgent({ state: 'Grace', graceDaysRemaining: 3 })).toBe(false);
    expect(isGracePeriodBannerUrgent({ state: 'Locked', graceDaysRemaining: 0 })).toBe(false);
    expect(isGracePeriodBannerUrgent(null)).toBe(false);
  });
});

describe('graceUrgentDismissStorageKey', () => {
  it('scopes dismiss by tenant and lock deadline', () => {
    expect(graceUrgentDismissStorageKey('t1', '2026-07-28T00:00:00.000Z')).toBe(
      'regkasse.license.graceUrgentDismissed.t1.2026-07-28T00:00:00.000Z'
    );
  });
});

describe('formatGraceLockCountdown', () => {
  it('includes seconds under 24h', () => {
    const nowMs = Date.parse('2026-07-27T12:00:00.000Z');
    const lockAt = '2026-07-27T18:30:45.000Z';
    expect(formatGraceLockCountdown(lockAt, nowMs)).toBe('6h 30m 45s');
  });

  it('returns zero label when already past', () => {
    const nowMs = Date.parse('2026-07-28T00:00:00.000Z');
    expect(formatGraceLockCountdown('2026-07-27T00:00:00.000Z', nowMs)).toBe('0h 0m 0s');
  });
});
