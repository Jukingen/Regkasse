/**
 * Fixed German (AT) date display for Admin UI — independent of text locale (de/en/tr).
 * API payloads must not use these helpers; display only.
 */

export const UI_DATE_PATTERN = 'DD.MM.YYYY' as const;
export const DAYJS_DATE_FORMAT = 'DD.MM.YYYY';
export const DAYJS_DATETIME_FORMAT = 'DD.MM.YYYY HH:mm';
export const DAYJS_DATETIME_SECONDS_FORMAT = 'DD.MM.YYYY HH:mm:ss';

export type FormatUserDateTimeOptions = {
  /** When true: `DD.MM.YYYY HH:mm:ss`; otherwise `DD.MM.YYYY HH:mm`. */
  includeSeconds?: boolean;
};

function parseInput(input: string | number | Date | null | undefined): Date | null {
  if (input == null || input === '') return null;
  const d = input instanceof Date ? input : new Date(input);
  return Number.isNaN(d.getTime()) ? null : d;
}

function pad2(value: number): string {
  return String(value).padStart(2, '0');
}

/** `01.12.2025` — device local timezone from ISO / Date. */
export function formatUserDate(input: string | number | Date | null | undefined): string {
  const d = parseInput(input);
  if (!d) return '';
  return `${pad2(d.getDate())}.${pad2(d.getMonth() + 1)}.${d.getFullYear()}`;
}

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
  options?: FormatUserDateTimeOptions,
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

/** `10:30` or `10:30:45`. */
export function formatUserTime(
  input: string | number | Date | null | undefined,
  options?: { includeSeconds?: boolean },
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

/** Maps personalization / DatePicker pattern (only DD.MM.YYYY supported in UI). */
export function toDayjsDateFormat(): string {
  return DAYJS_DATE_FORMAT;
}
