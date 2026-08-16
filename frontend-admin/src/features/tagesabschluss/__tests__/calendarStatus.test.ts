import { describe, expect, it } from 'vitest';

import {
  CalendarDayStatus,
  calendarStatusTooltipKey,
  resolveCalendarDayStatus,
  summarizeCalendarMonth,
} from '../calendarStatus';

describe('tagesabschluss calendarStatus', () => {
  it('maps closed normal, empty, open, no-tx, and future', () => {
    expect(resolveCalendarDayStatus({ date: '2026-05-01', isClosed: true, dayKind: 'normal' })).toBe(
      CalendarDayStatus.Closed
    );
    expect(resolveCalendarDayStatus({ date: '2026-05-01', isClosed: true, dayKind: 'empty' })).toBe(
      CalendarDayStatus.Empty
    );
    expect(
      resolveCalendarDayStatus({ date: '2026-05-01', isClosed: true, closingType: 'empty' })
    ).toBe(CalendarDayStatus.Empty);
    expect(
      resolveCalendarDayStatus({ date: '2026-05-02', isClosed: false, transactionCount: 3 })
    ).toBe(CalendarDayStatus.Open);
    expect(
      resolveCalendarDayStatus({ date: '2026-05-03', isClosed: false, transactionCount: 0 })
    ).toBe(CalendarDayStatus.NoTransactions);
    expect(resolveCalendarDayStatus({ date: '2026-12-31', isFuture: true, transactionCount: 0 })).toBe(
      CalendarDayStatus.Future
    );
  });

  it('summarizes month cells and exposes tooltip keys', () => {
    const summary = summarizeCalendarMonth([
      { date: '2026-05-01', isClosed: true, dayKind: 'normal' },
      { date: '2026-05-02', isClosed: true, dayKind: 'empty' },
      { date: '2026-05-03', isClosed: false, transactionCount: 4 },
      { date: '2026-05-04', isClosed: false, transactionCount: 0 },
      { date: '2026-05-05', isFuture: true },
    ]);
    expect(summary).toEqual({
      totalDays: 5,
      closedDays: 2,
      emptyClosedDays: 1,
      openDays: 1,
      noTransactionDays: 1,
      futureDays: 1,
    });
    expect(calendarStatusTooltipKey(CalendarDayStatus.Closed)).toBe(
      'tagesabschluss.calendar.tooltip.closed'
    );
    expect(calendarStatusTooltipKey(CalendarDayStatus.Open)).toBe(
      'tagesabschluss.calendar.tooltip.open'
    );
  });
});
