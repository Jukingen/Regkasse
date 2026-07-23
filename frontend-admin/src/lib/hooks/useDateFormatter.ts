'use client';

import { useMemo } from 'react';

import {
  type FormatUserDateTimeOptions,
  formatUserMonthDay,
  formatWithUserPreferences,
  toDayjsDateFormat,
  toDayjsDateTimeFormat,
} from '@/lib/dateFormatter';
import { usePersonalization } from '@/lib/personalization/PersonalizationProvider';

const EMPTY_DISPLAY = '—';

function withEmptyDisplay(value: string): string {
  return value || EMPTY_DISPLAY;
}

/** Preference-aware date display helpers for Admin UI. */
export function useDateFormatter() {
  const { preferences } = usePersonalization();
  const prefs = useMemo(
    () => ({
      dateFormat: preferences.dateFormat,
      timeFormat: preferences.timeFormat,
      timeZone: preferences.timeZone,
    }),
    [preferences.dateFormat, preferences.timeFormat, preferences.timeZone]
  );

  return useMemo(
    () => ({
      formatDate: (input: Parameters<typeof formatWithUserPreferences>[0]) =>
        withEmptyDisplay(formatWithUserPreferences(input, prefs)),
      formatDateTime: (
        input: Parameters<typeof formatWithUserPreferences>[0],
        options?: FormatUserDateTimeOptions
      ) =>
        withEmptyDisplay(
          formatWithUserPreferences(input, prefs, { ...options, withTime: true })
        ),
      formatDateTimeWithSeconds: (input: Parameters<typeof formatWithUserPreferences>[0]) =>
        withEmptyDisplay(
          formatWithUserPreferences(input, prefs, { withTime: true, includeSeconds: true })
        ),
      formatMonthDay: (input: Parameters<typeof formatUserMonthDay>[0]) =>
        withEmptyDisplay(formatUserMonthDay(input)),
      dayjsDateFormat: toDayjsDateFormat(prefs.dateFormat),
      dayjsDateTimeFormat: toDayjsDateTimeFormat(prefs.dateFormat, prefs.timeFormat),
      dayjsDateTimeSecondsFormat: `${toDayjsDateTimeFormat(prefs.dateFormat, prefs.timeFormat)}:ss`,
      preferences: prefs,
    }),
    [prefs]
  );
}
