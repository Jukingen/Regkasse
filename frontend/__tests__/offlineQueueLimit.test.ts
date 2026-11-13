import { describe, expect, it } from '@jest/globals';

import {
  isOfflineQueueAtCap,
  isOfflineQueueLimitFailure,
  mergeOfflineTenantLimit,
  offlineLimitPercent,
  resolveOfflineLimitBarTone,
  shouldShowOfflineLimitReachedModal,
} from '../utils/offlineQueueLimit';

describe('offlineQueueLimit', () => {
  it('maps 0/50/80/100 percent to bar tones', () => {
    expect(offlineLimitPercent(0, 50)).toBe(0);
    expect(resolveOfflineLimitBarTone(0)).toBe('green');
    expect(offlineLimitPercent(25, 50)).toBe(50);
    expect(resolveOfflineLimitBarTone(50)).toBe('green');
    expect(offlineLimitPercent(40, 50)).toBe(80);
    expect(resolveOfflineLimitBarTone(80)).toBe('yellow');
    expect(offlineLimitPercent(50, 50)).toBe(100);
    expect(resolveOfflineLimitBarTone(100)).toBe('red');
  });

  it('treats current >= limit as cap', () => {
    expect(isOfflineQueueAtCap(0, 50)).toBe(false);
    expect(isOfflineQueueAtCap(49, 50)).toBe(false);
    expect(isOfflineQueueAtCap(50, 50)).toBe(true);
    expect(isOfflineQueueAtCap(51, 50)).toBe(true);
  });

  it('merges local pending with server usage', () => {
    expect(mergeOfflineTenantLimit({ current: 10, limit: 50, approachingLimit: false, limitReached: false }, 12)).toEqual({
      current: 12,
      limit: 50,
      approachingLimit: false,
      limitReached: false,
    });
  });

  it('recognizes maxOfflineTransactions LIMIT_EXCEEDED', () => {
    expect(
      isOfflineQueueLimitFailure({
        error: 'LIMIT_EXCEEDED',
        limitKey: 'maxOfflineTransactions',
      })
    ).toBe(true);
    expect(
      isOfflineQueueLimitFailure({
        error: 'LIMIT_EXCEEDED',
        limitKey: 'dailyMaxTransactions',
      })
    ).toBe(false);
  });

  it('shows the blocking modal at 100% and on 409 LIMIT_EXCEEDED', () => {
    expect(shouldShowOfflineLimitReachedModal({ limitReached: true })).toBe(true);
    expect(
      shouldShowOfflineLimitReachedModal({
        status: 409,
        error: 'LIMIT_EXCEEDED',
        limitKey: 'maxOfflineTransactions',
      })
    ).toBe(true);
    expect(shouldShowOfflineLimitReachedModal({ limitReached: false })).toBe(false);
  });
});
