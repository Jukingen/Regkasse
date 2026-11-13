'use client';

/**
 * i18n-aware date formatting for Admin UI.
 *
 * Uses the custom {@link useI18n} catalog (not react-i18next) and Day.js patterns
 * from `common.dateFormat.*`.
 */
import { useCallback, useMemo } from 'react';

import { useI18n } from '@/i18n/I18nProvider';
import {
  type DateFormatKey,
  DATE_FORMAT_FALLBACKS,
  type FormatLocalizedDateOptions,
  formatLocalizedDate,
  resolveDateFormatString,
} from '@/lib/formattedDate';
import { EMPTY_DATE_DISPLAY, type DateInput } from '@/lib/dateUtils';

export type { DateFormatKey };
export { DATE_FORMAT_FALLBACKS };

export function useFormattedDate() {
  const { t, textLocale } = useI18n();

  const getFormatString = useCallback(
    (formatKey: DateFormatKey): string => resolveDateFormatString(t, formatKey),
    [t]
  );

  const format = useCallback(
    (
      date: DateInput,
      formatKey: DateFormatKey = 'short',
      options?: FormatLocalizedDateOptions
    ): string => formatLocalizedDate(date, formatKey, textLocale, t, options),
    [t, textLocale]
  );

  return useMemo(
    () => ({
      format,
      getFormatString,
      textLocale,
      emptyDisplay: EMPTY_DATE_DISPLAY,
    }),
    [format, getFormatString, textLocale]
  );
}
