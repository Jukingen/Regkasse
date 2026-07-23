/**
 * Day.js locale + display helpers for Admin UI (de / en / tr).
 *
 * Numeric display defaults stay German AT (`DD.MM.YYYY`) across text locales.
 * Month/weekday/relative strings follow `setDateLocale` (synced from I18nProvider).
 *
 * API query params and export filenames: use `formatIsoDate` / `ISO_DATE_FORMAT` (YYYY-MM-DD).
 */
import dayjs, { type Dayjs } from '@/lib/dayjs';
import 'dayjs/locale/de';
import 'dayjs/locale/en';
import 'dayjs/locale/tr';

export const SUPPORTED_DATE_LOCALES = ['de', 'en', 'tr'] as const;
export type DateLocale = (typeof SUPPORTED_DATE_LOCALES)[number];

export const EMPTY_DATE_DISPLAY = '—' as const;

/** UI date — German AT. */
export const DATE_FORMAT = 'DD.MM.YYYY' as const;
/** UI date-time without seconds. */
export const DATETIME_FORMAT = 'DD.MM.YYYY HH:mm' as const;
/** UI date-time with seconds. */
export const DATETIME_SECONDS_FORMAT = 'DD.MM.YYYY HH:mm:ss' as const;
/** API / export filename date (ISO calendar date). */
export const ISO_DATE_FORMAT = 'YYYY-MM-DD' as const;

const DEFAULT_DATE_LOCALE: DateLocale = 'de';

export type DateInput = string | number | Date | Dayjs | null | undefined;

function isSupportedDateLocale(locale: string): locale is DateLocale {
  return (SUPPORTED_DATE_LOCALES as readonly string[]).includes(locale);
}

function toDayjs(date: DateInput, utc = false): Dayjs | null {
  if (date == null || date === '') return null;
  const parsed = utc ? dayjs.utc(date) : dayjs(date);
  return parsed.isValid() ? parsed : null;
}

/** Dynamically set Day.js locale based on app language (default: de). */
export function setDateLocale(locale: string): DateLocale {
  const localeToUse = isSupportedDateLocale(locale) ? locale : DEFAULT_DATE_LOCALE;
  dayjs.locale(localeToUse);
  return localeToUse;
}

/** Current Day.js global locale (e.g. for tests / diagnostics). */
export function getDateLocale(): string {
  return dayjs.locale();
}

/** `15.07.2026` — device-local; invalid → `—`. */
export function formatDate(date: DateInput, format: string = DATE_FORMAT): string {
  const parsed = toDayjs(date);
  return parsed ? parsed.format(format) : EMPTY_DATE_DISPLAY;
}

/** `15.07.2026 14:30` — device-local; invalid → `—`. */
export function formatDateTime(date: DateInput, format: string = DATETIME_FORMAT): string {
  const parsed = toDayjs(date);
  return parsed ? parsed.format(format) : EMPTY_DATE_DISPLAY;
}

/** `15.07.2026 14:30:45` — device-local; invalid → `—`. */
export function formatDateTimeSeconds(
  date: DateInput,
  format: string = DATETIME_SECONDS_FORMAT
): string {
  const parsed = toDayjs(date);
  return parsed ? parsed.format(format) : EMPTY_DATE_DISPLAY;
}

/** `14:30` / `14:30:45` — device-local time only. */
export function formatTime(date: DateInput, format: string = 'HH:mm:ss'): string {
  const parsed = toDayjs(date);
  return parsed ? parsed.format(format) : EMPTY_DATE_DISPLAY;
}

/** UTC wall time for API timestamps stamped in UTC. */
export function formatUtcDateTime(
  date: DateInput,
  format: string = DATETIME_SECONDS_FORMAT
): string {
  const parsed = toDayjs(date, true);
  return parsed ? parsed.format(format) : EMPTY_DATE_DISPLAY;
}

/** `Juli 2026` / `July 2026` — follows active Day.js locale. */
export function formatMonthYear(date: DateInput, format: string = 'MMMM YYYY'): string {
  const parsed = toDayjs(date);
  return parsed ? parsed.format(format) : EMPTY_DATE_DISPLAY;
}

/**
 * ISO calendar date for API params and export filenames (`2026-07-15`).
 * Invalid → empty string (callers must not invent a date).
 */
export function formatIsoDate(date: DateInput): string {
  const parsed = toDayjs(date);
  return parsed ? parsed.format(ISO_DATE_FORMAT) : '';
}

/** Export / download filename stamp — keep ISO for consistency across locales. */
export function formatExportFilenameDate(date: DateInput = dayjs()): string {
  return formatIsoDate(date) || dayjs().format(ISO_DATE_FORMAT);
}

export function getWeekdayNames(short: boolean = true): string[] {
  const now = dayjs();
  const days: string[] = [];
  for (let i = 0; i < 7; i++) {
    const day = now.startOf('week').add(i, 'day');
    days.push(short ? day.format('dd') : day.format('dddd'));
  }
  return days;
}

/**
 * Localized weekday labels in ISO order (Mon=0 … Sun=6).
 * Prefer this for heatmaps / calendars that use Monday-first grids.
 */
export function getIsoWeekdayNames(
  locale: string = DEFAULT_DATE_LOCALE,
  short: boolean = true
): string[] {
  const localeToUse = isSupportedDateLocale(locale) ? locale : DEFAULT_DATE_LOCALE;
  const format = short ? 'dd' : 'dddd';
  return Array.from({ length: 7 }, (_, i) =>
    dayjs().locale(localeToUse).isoWeekday(i + 1).format(format)
  );
}

/** Catalog weekday keys in ISO order (Mon … Sun) for `calendar.weekday.*`. */
export const ISO_WEEKDAY_I18N_KEYS = [
  'monday',
  'tuesday',
  'wednesday',
  'thursday',
  'friday',
  'saturday',
  'sunday',
] as const;

// Default Admin UI language is German — apply before any relative/month formatting.
setDateLocale(DEFAULT_DATE_LOCALE);

export default dayjs;
