'use client';

import { useMemo } from 'react';
import {
  DAYJS_DATE_FORMAT,
  DAYJS_DATETIME_FORMAT,
  DAYJS_DATETIME_SECONDS_FORMAT,
  formatUserDate,
  formatUserDateTime,
  formatUserMonthDay,
  type FormatUserDateTimeOptions,
} from '@/lib/dateFormatter';
const EMPTY_DISPLAY = '—';

function withEmptyDisplay(value: string): string {
  return value || EMPTY_DISPLAY;
}

/** German date display helpers for Admin UI (locale-independent). */
export function useDateFormatter() {
  return useMemo(
    () => ({
      formatDate: (input: Parameters<typeof formatUserDate>[0]) => withEmptyDisplay(formatUserDate(input)),
      formatDateTime: (
        input: Parameters<typeof formatUserDateTime>[0],
        options?: FormatUserDateTimeOptions,
      ) => withEmptyDisplay(formatUserDateTime(input, options)),
      formatDateTimeWithSeconds: (input: Parameters<typeof formatUserDateTime>[0]) =>
        withEmptyDisplay(formatUserDateTime(input, { includeSeconds: true })),
      formatMonthDay: (input: Parameters<typeof formatUserMonthDay>[0]) =>
        withEmptyDisplay(formatUserMonthDay(input)),
      dayjsDateFormat: DAYJS_DATE_FORMAT,
      dayjsDateTimeFormat: DAYJS_DATETIME_FORMAT,
      dayjsDateTimeSecondsFormat: DAYJS_DATETIME_SECONDS_FORMAT,
    }),
    [],
  );
}
