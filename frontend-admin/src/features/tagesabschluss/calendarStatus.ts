export enum CalendarDayStatus {
  Closed = 'closed',
  Empty = 'empty',
  Open = 'open',
  NoTransactions = 'no-transactions',
  Future = 'future',
  Today = 'today',
}

export type CalendarDayFields = {
  date: string;
  isClosed?: boolean;
  dayKind?: string | null;
  closingType?: string | null;
  transactionCount?: number;
  canClose?: boolean;
  isToday?: boolean;
  isFuture?: boolean;
};

export type CalendarMonthSummary = {
  totalDays: number;
  closedDays: number;
  emptyClosedDays: number;
  openDays: number;
  noTransactionDays: number;
  futureDays: number;
};

function isEmptyKind(day: CalendarDayFields): boolean {
  const kind = (day.dayKind ?? day.closingType ?? '').toLowerCase();
  return kind === 'empty';
}

/**
 * Exclusive status for fill color. Today is an overlay (`isToday`) unless the day is in the future.
 */
export function resolveCalendarDayStatus(day: CalendarDayFields): CalendarDayStatus {
  if (day.isFuture === true) return CalendarDayStatus.Future;
  if (day.isClosed) {
    return isEmptyKind(day) ? CalendarDayStatus.Empty : CalendarDayStatus.Closed;
  }
  if ((day.transactionCount ?? 0) > 0) return CalendarDayStatus.Open;
  return CalendarDayStatus.NoTransactions;
}

export function calendarStatusTooltipKey(status: CalendarDayStatus): string {
  switch (status) {
    case CalendarDayStatus.Closed:
      return 'tagesabschluss.calendar.tooltip.closed';
    case CalendarDayStatus.Empty:
      return 'tagesabschluss.calendar.tooltip.empty';
    case CalendarDayStatus.Open:
      return 'tagesabschluss.calendar.tooltip.open';
    case CalendarDayStatus.NoTransactions:
      return 'tagesabschluss.calendar.tooltip.noTransactions';
    case CalendarDayStatus.Future:
      return 'tagesabschluss.calendar.tooltip.future';
    case CalendarDayStatus.Today:
      return 'tagesabschluss.calendar.tooltip.today';
  }
}

export function summarizeCalendarMonth(days: CalendarDayFields[]): CalendarMonthSummary {
  const summary: CalendarMonthSummary = {
    totalDays: days.length,
    closedDays: 0,
    emptyClosedDays: 0,
    openDays: 0,
    noTransactionDays: 0,
    futureDays: 0,
  };
  for (const day of days) {
    const status = resolveCalendarDayStatus(day);
    switch (status) {
      case CalendarDayStatus.Closed:
        summary.closedDays += 1;
        break;
      case CalendarDayStatus.Empty:
        summary.closedDays += 1;
        summary.emptyClosedDays += 1;
        break;
      case CalendarDayStatus.Open:
        summary.openDays += 1;
        break;
      case CalendarDayStatus.NoTransactions:
        summary.noTransactionDays += 1;
        break;
      case CalendarDayStatus.Future:
        summary.futureDays += 1;
        break;
      default:
        break;
    }
  }
  return summary;
}

export function calendarDateKey(date: string | Date): string {
  if (typeof date === 'string') return date.slice(0, 10);
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}
