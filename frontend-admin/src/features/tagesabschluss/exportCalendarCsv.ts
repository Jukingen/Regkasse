import type { DailyClosingCalendarDay } from '@/features/tagesabschluss/api/dailyClosingCalendar';
import { rowsToCsv, downloadCsvText } from '@/shared/utils/csv';

import {
  CalendarDayStatus,
  resolveCalendarDayStatus,
} from './calendarStatus';

export type CalendarCsvLabels = {
  date: string;
  status: string;
  transactionCount: string;
  canClose: string;
  closed: string;
  empty: string;
  open: string;
  noTransactions: string;
  future: string;
};

function statusLabel(status: CalendarDayStatus, labels: CalendarCsvLabels): string {
  switch (status) {
    case CalendarDayStatus.Closed:
      return labels.closed;
    case CalendarDayStatus.Empty:
      return labels.empty;
    case CalendarDayStatus.Open:
      return labels.open;
    case CalendarDayStatus.NoTransactions:
      return labels.noTransactions;
    case CalendarDayStatus.Future:
      return labels.future;
    default:
      return status;
  }
}

export function exportDailyClosingCalendarCsv(
  days: DailyClosingCalendarDay[],
  fileName: string,
  labels: CalendarCsvLabels
): void {
  const matrix: unknown[][] = [
    [labels.date, labels.status, labels.transactionCount, labels.canClose],
    ...days.map((day) => {
      const status = resolveCalendarDayStatus(day);
      return [
        String(day.date ?? '').slice(0, 10),
        statusLabel(status, labels),
        day.transactionCount,
        day.canClose ? '1' : '0',
      ];
    }),
  ];
  downloadCsvText(rowsToCsv(matrix), fileName);
}
