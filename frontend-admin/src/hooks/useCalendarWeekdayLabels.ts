'use client';

/**
 * Localized calendar weekday/month labels for Admin UI.
 * Uses Day.js for display (ISO Mon-first) and optional i18n catalog keys.
 */
import { useMemo } from 'react';

import { useI18n } from '@/i18n/I18nProvider';
import {
  getIsoWeekdayNames,
  ISO_WEEKDAY_I18N_KEYS,
} from '@/lib/dateUtils';

export type UseCalendarLabelsOptions = {
  /** Prefer explicit i18n catalog (`calendar.weekday.*`) over Day.js. Default: false (Day.js). */
  preferCatalog?: boolean;
  short?: boolean;
};

/**
 * Monday-first short weekday labels for heatmaps / calendar grids.
 */
export function useCalendarWeekdayLabels(options?: UseCalendarLabelsOptions): string[] {
  const { t, textLocale } = useI18n();
  const short = options?.short !== false;
  const preferCatalog = options?.preferCatalog === true;

  return useMemo(() => {
    if (preferCatalog) {
      const ns = short ? 'calendar.weekday' : 'calendar.weekdayLong';
      return ISO_WEEKDAY_I18N_KEYS.map((key) => t(`${ns}.${key}`));
    }
    return getIsoWeekdayNames(textLocale, short);
  }, [preferCatalog, short, t, textLocale]);
}
