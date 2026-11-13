import { describe, expect, it } from 'vitest';

import {
  resolveDailyClosingTodayTone,
  weekClosingPercent,
} from '../dailyClosingWidgetStatus';

describe('dailyClosingWidgetStatus', () => {
  it('maps today closed / open / empty tones', () => {
    expect(resolveDailyClosingTodayTone({ isClosed: true, transactionCount: 4 })).toBe('closed');
    expect(resolveDailyClosingTodayTone({ isClosed: false, transactionCount: 3 })).toBe('open');
    expect(resolveDailyClosingTodayTone({ isClosed: false, transactionCount: 0 })).toBe('empty');
  });

  it('computes week progress percent', () => {
    expect(weekClosingPercent(3, 7)).toBe(43);
    expect(weekClosingPercent(7, 7)).toBe(100);
    expect(weekClosingPercent(0, 7)).toBe(0);
    expect(weekClosingPercent(1, 0)).toBe(0);
  });
});
