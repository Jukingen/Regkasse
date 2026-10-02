import { describe, expect, it } from '@jest/globals';

import {
  formatRetryCountdownLabel,
  resolveOfflineQueueDot,
  type OfflineQueueDotColor,
} from '../utils/offlineQueueIndicator';

describe('resolveOfflineQueueDot', () => {
  const cases: {
    name: string;
    input: Parameters<typeof resolveOfflineQueueDot>[0];
    expected: OfflineQueueDotColor;
  }[] = [
    {
      name: 'green when online and queue empty',
      input: { isOnline: true, pendingCount: 0, failedCount: 0, isSyncing: false },
      expected: 'green',
    },
    {
      name: 'yellow when online with items syncing',
      input: { isOnline: true, pendingCount: 3, failedCount: 0, isSyncing: true },
      expected: 'yellow',
    },
    {
      name: 'yellow when online with pending items',
      input: { isOnline: true, pendingCount: 2, failedCount: 0, isSyncing: false },
      expected: 'yellow',
    },
    {
      name: 'orange when offline with queued items',
      input: { isOnline: false, pendingCount: 4, failedCount: 0, isSyncing: false },
      expected: 'orange',
    },
    {
      name: 'red when sync failed and items stuck',
      input: { isOnline: true, pendingCount: 1, failedCount: 2, isSyncing: false },
      expected: 'red',
    },
    {
      name: 'red wins over offline when items are stuck',
      input: { isOnline: false, pendingCount: 0, failedCount: 1, isSyncing: false },
      expected: 'red',
    },
  ];

  it.each(cases)('$name', ({ input, expected }) => {
    expect(resolveOfflineQueueDot(input)).toBe(expected);
  });

  it('formats retry countdown', () => {
    expect(formatRetryCountdownLabel(5000)).toBe('5s');
    expect(formatRetryCountdownLabel(65_000)).toBe('1m 05s');
  });
});
