import { describe, expect, it } from '@jest/globals';

import { resolveOfflineUiLevel, type OfflineUiLevel } from '../utils/offlineStatusLevel';

describe('resolveOfflineUiLevel', () => {
  const cases: {
    name: string;
    input: Parameters<typeof resolveOfflineUiLevel>[0];
    expected: OfflineUiLevel;
  }[] = [
    {
      name: 'online when connected and queue empty',
      input: { isOnline: true, pendingCount: 0, hoursRemaining: 72 },
      expected: 'online',
    },
    {
      name: 'offline when disconnected',
      input: { isOnline: false, pendingCount: 0, hoursRemaining: 72 },
      expected: 'offline',
    },
    {
      name: 'warning at 40 pending',
      input: { isOnline: true, pendingCount: 40, hoursRemaining: 48 },
      expected: 'warning',
    },
    {
      name: 'critical at 48 pending',
      input: { isOnline: true, pendingCount: 48, hoursRemaining: 48 },
      expected: 'critical',
    },
    {
      name: 'critical when under 24 hours with pending',
      input: { isOnline: true, pendingCount: 5, hoursRemaining: 23 },
      expected: 'critical',
    },
    {
      name: 'online when under warning thresholds',
      input: { isOnline: true, pendingCount: 39, hoursRemaining: 24 },
      expected: 'online',
    },
  ];

  it.each(cases)('$name', ({ input, expected }) => {
    expect(resolveOfflineUiLevel(input)).toBe(expected);
  });
});
