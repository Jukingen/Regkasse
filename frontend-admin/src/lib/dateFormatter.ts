/**
 * FA date/time display helpers.
 * Default helpers stay German (AT) for fiscal-facing consistency.
 * Preference-aware formatting lives in {@link formatWithUserPreferences} / {@link useDateFormatter}.
 */

import type { DateFormatPattern, TimeFormatPreference, UserTimeZone } from '@/lib/personalization/types';

export const UI_DATE_PATTERN = 'DD.MM.YYYY' as const;
export const DAYJS_DATE_FORMAT = 'DD.MM.YYYY';
export const DAYJS_DATETIME_FORMAT = 'DD.MM.YYYY HH:mm';
export const DAYJS_DATETIME_SECONDS_FORMAT = 'DD.MM.YYYY HH:mm:ss';

/** Empty / invalid display placeholder for German format helpers. */
export const GERMAN_DATE_EMPTY = '—' as const;

export type FormatUserDateTimeOptions = {
  /** When true: includes seconds. */
  includeSeconds?: boolean;
};

export type UserFormatPreferences = {
  dateFormat: DateFormatPattern;
  timeFormat: TimeFormatPreference;
  timeZone: UserTimeZone;
};

function parseInput(input: string | number | Date | null | undefined): Date | null {
  if (input == null || input === '') return null;
  const d = input instanceof Date ? input : new Date(input);
  return Number.isNaN(d.getTime()) ? null : d;
}

function pad2(value: number): string {
  return String(value).padStart(2, '0');
}

function partsInZone(date: Date, timeZone: string): {
  year: number;
  month: number;
  day: number;
  hour: number;
  minute: number;
  second: number;
} {
  const fmt = new Intl.DateTimeFormat('en-US', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23',
  });
  const map = Object.fromEntries(
    fmt.formatToParts(date).filter((p) => p.type !== 'literal').map((p) => [p.type, p.value])
  );
  return {
    year: Number(map.year),
    month: Number(map.month),
    day: Number(map.day),
    hour: Number(map.hour),
    minute: Number(map.minute),
    second: Number(map.second),
  };
}

function formatDateParts(
  year: number,
  month: number,
  day: number,
  dateFormat: DateFormatPattern
): string {
  const y = String(year);
  const m = pad2(month);
  const d = pad2(day);
  switch (dateFormat) {
    case 'MM/DD/YYYY':
      return `${m}/${d}/${y}`;
    case 'YYYY-MM-DD':
      return `${y}-${m}-${d}`;
    case 'DD.MM.YYYY':
    default:
      return `${d}.${m}.${y}`;
  }
}

function formatTimeParts(
  hour: number,
  minute: number,
  second: number | undefined,
  timeFormat: TimeFormatPreference,
  includeSeconds: boolean
): string {
  if (timeFormat === '12h') {
    const period = hour >= 12 ? 'PM' : 'AM';
    const h12 = hour % 12 === 0 ? 12 : hour % 12;
    const base = `${pad2(h12)}:${pad2(minute)}`;
    return includeSeconds && second != null
      ? `${base}:${pad2(second)} ${period}`
      : `${base} ${period}`;
  }
  const base = `${pad2(hour)}:${pad2(minute)}`;
  return includeSeconds && second != null ? `${base}:${pad2(second)}` : base;
}

/** Preference-aware date formatting (timezone + pattern). */
export function formatWithUserPreferences(
  input: string | number | Date | null | undefined,
  prefs: UserFormatPreferences,
  options?: FormatUserDateTimeOptions & { withTime?: boolean }
): string {
  const d = parseInput(input);
  if (!d) return '';
  const p = partsInZone(d, prefs.timeZone);
  const datePart = formatDateParts(p.year, p.month, p.day, prefs.dateFormat);
  if (!options?.withTime) return datePart;
  const timePart = formatTimeParts(
    p.hour,
    p.minute,
    p.second,
    prefs.timeFormat,
    options.includeSeconds === true
  );
  return `${datePart} ${timePart}`;
}

export function toDayjsDateFormat(pattern: DateFormatPattern = 'DD.MM.YYYY'): string {
  return pattern;
}

export function toDayjsDateTimeFormat(
  pattern: DateFormatPattern = 'DD.MM.YYYY',
  timeFormat: TimeFormatPreference = '24h'
): string {
  if (timeFormat === '12h') return `${pattern} hh:mm A`;
  return `${pattern} HH:mm`;
}

/** `01.12.2025` — device local timezone from ISO / Date. */
export function formatUserDate(input: string | number | Date | null | undefined): string {
  const d = parseInput(input);
  if (!d) return '';
  return `${pad2(d.getDate())}.${pad2(d.getMonth() + 1)}.${d.getFullYear()}`;
}

/** Alias used by billing overview and legacy callers. */
export const formatDate = formatUserDate;

/** `01.12.` — chart axis labels without year. */
export function formatUserMonthDay(input: string | number | Date | null | undefined): string {
  const d = parseInput(input);
  if (!d) return '';
  return `${pad2(d.getDate())}.${pad2(d.getMonth() + 1)}.`;
}

/** `06.2025` — month period labels (Monatsbericht). */
export function formatUserMonthYear(input: string | number | Date | null | undefined): string {
  const d = parseInput(input);
  if (d) return `${pad2(d.getMonth() + 1)}.${d.getFullYear()}`;
  if (typeof input === 'string') {
    const match = /^(\d{4})-(\d{2})/.exec(input);
    if (match) return `${match[2]}.${match[1]}`;
  }
  return '';
}

/** `01.12.2025 10:30` or with seconds when requested. */
export function formatUserDateTime(
  input: string | number | Date | null | undefined,
  options?: FormatUserDateTimeOptions
): string {
  const d = parseInput(input);
  if (!d) return '';
  const datePart = formatUserDate(d);
  const h = pad2(d.getHours());
  const m = pad2(d.getMinutes());
  if (options?.includeSeconds) {
    return `${datePart} ${h}:${m}:${pad2(d.getSeconds())}`;
  }
  return `${datePart} ${h}:${m}`;
}

/** Alias for table columns — `DD.MM.YYYY HH:mm`. */
export const formatDateTime = formatUserDateTime;

/** `10:30` or `10:30:45`. */
export function formatUserTime(
  input: string | number | Date | null | undefined,
  options?: { includeSeconds?: boolean }
): string {
  const d = parseInput(input);
  if (!d) return '';
  const h = pad2(d.getHours());
  const m = pad2(d.getMinutes());
  if (options?.includeSeconds) {
    return `${h}:${m}:${pad2(d.getSeconds())}`;
  }
  return `${h}:${m}`;
}

/**
 * German UI datetime — `DD.MM.YYYY HH:mm`, or `—` when missing/invalid.
 * Prefer over raw `toLocaleString` so Admin stays consistent across de/en/tr text locales.
 */
export function formatGermanDateTime(date: string | Date | null | undefined): string {
  return formatUserDateTime(date) || GERMAN_DATE_EMPTY;
}

/** German UI date — `DD.MM.YYYY`, or `—` when missing/invalid. */
export function formatGermanDate(date: string | Date | null | undefined): string {
  return formatUserDate(date) || GERMAN_DATE_EMPTY;
}

/** German UI time — `HH:mm`, or `—` when missing/invalid. */
export function formatGermanTime(date: string | Date | null | undefined): string {
  return formatUserTime(date) || GERMAN_DATE_EMPTY;
}
