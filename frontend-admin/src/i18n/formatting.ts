/**
 * Number/currency: `formatLocale` (de-AT, en-US, tr-TR).
 * Dates: fixed German display `DD.MM.YYYY` via `@/lib/dateFormatter` — independent of text locale.
 *
 * @example
 *   const { formatLocale } = useI18n();
 *   formatCurrency(19.99, formatLocale);
 *   formatDate(iso, formatLocale); // → 01.12.2025 (locale ignored for dates)
 */
import { formatUserDate, formatUserDateTime, formatUserMonthDay } from '@/lib/dateFormatter';

export const FORMAT_EMPTY_DISPLAY = '—';

function wantsMonthDayOnly(options?: Intl.DateTimeFormatOptions): boolean {
  if (!options) return false;
  const hasMonth = options.month !== undefined;
  const hasDay = options.day !== undefined;
  const hasYear = options.year !== undefined;
  return hasMonth && hasDay && !hasYear;
}

function wantsDateTimeSeconds(options?: Intl.DateTimeFormatOptions): boolean {
  if (!options) return false;
  return (
    options.second === '2-digit' ||
    options.timeStyle === 'medium' ||
    options.timeStyle === 'long' ||
    options.timeStyle === 'full'
  );
}

function hasTimeComponent(options?: Intl.DateTimeFormatOptions): boolean {
  if (!options) return false;
  return (
    options.timeStyle !== undefined ||
    options.hour !== undefined ||
    options.minute !== undefined ||
    options.second !== undefined
  );
}

export function formatDateTime(
  input: string | number | Date | null | undefined,
  _formatLocale?: string,
  options?: Intl.DateTimeFormatOptions
): string {
  if (input == null || input === '') return FORMAT_EMPTY_DISPLAY;
  if (wantsMonthDayOnly(options)) {
    const short = formatUserMonthDay(input);
    return short || FORMAT_EMPTY_DISPLAY;
  }
  if (hasTimeComponent(options)) {
    const formatted = formatUserDateTime(input, { includeSeconds: wantsDateTimeSeconds(options) });
    return formatted || FORMAT_EMPTY_DISPLAY;
  }
  if (options?.dateStyle !== undefined) {
    const dateOnly = formatUserDate(input);
    return dateOnly || FORMAT_EMPTY_DISPLAY;
  }
  const formatted = formatUserDateTime(input, { includeSeconds: wantsDateTimeSeconds(options) });
  return formatted || FORMAT_EMPTY_DISPLAY;
}

export function formatDate(
  input: string | number | Date | null | undefined,
  _formatLocale?: string,
  options?: Intl.DateTimeFormatOptions
): string {
  if (input == null || input === '') return FORMAT_EMPTY_DISPLAY;
  if (wantsMonthDayOnly(options)) {
    const short = formatUserMonthDay(input);
    return short || FORMAT_EMPTY_DISPLAY;
  }
  const formatted = formatUserDate(input);
  return formatted || FORMAT_EMPTY_DISPLAY;
}

export function formatNumber(
  value: number,
  formatLocale: string,
  options?: Intl.NumberFormatOptions
): string {
  if (!Number.isFinite(value)) return FORMAT_EMPTY_DISPLAY;
  return value.toLocaleString(formatLocale, options);
}

export type FormatCurrencyOptions = {
  currency?: string;
  minimumFractionDigits?: number;
  maximumFractionDigits?: number;
};

export function formatCurrency(
  value: number,
  formatLocale: string,
  options?: FormatCurrencyOptions
): string {
  if (!Number.isFinite(value)) return FORMAT_EMPTY_DISPLAY;
  const currency = options?.currency ?? 'EUR';
  const minimumFractionDigits = options?.minimumFractionDigits ?? 2;
  const maximumFractionDigits = options?.maximumFractionDigits ?? 2;
  return value.toLocaleString(formatLocale, {
    style: 'currency',
    currency,
    minimumFractionDigits,
    maximumFractionDigits,
  });
}

export type FormatPercentOptions = {
  minimumFractionDigits?: number;
  maximumFractionDigits?: number;
};

/** Oran 0–1 (örn. 0,25 → %25). */
export function formatPercent(
  value: number,
  formatLocale: string,
  options?: FormatPercentOptions
): string {
  if (!Number.isFinite(value)) return FORMAT_EMPTY_DISPLAY;
  return value.toLocaleString(formatLocale, {
    style: 'percent',
    minimumFractionDigits: options?.minimumFractionDigits ?? 0,
    maximumFractionDigits: options?.maximumFractionDigits ?? 2,
  });
}

export function formatBytes(bytes: number, formatLocale: string): string {
  if (!Number.isFinite(bytes) || bytes < 0) return FORMAT_EMPTY_DISPLAY;
  if (bytes === 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB'] as const;
  const exponent = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1);
  const value = bytes / 1024 ** exponent;
  return `${value.toLocaleString(formatLocale, { maximumFractionDigits: exponent === 0 ? 0 : 1 })} ${units[exponent]}`;
}

export function createIntlFormatters(formatLocale: string) {
  return {
    formatCurrency: (value: number, opts?: FormatCurrencyOptions) =>
      formatCurrency(value, formatLocale, opts),
    formatDate: (input: Parameters<typeof formatDate>[0], opts?: Intl.DateTimeFormatOptions) =>
      formatDate(input, formatLocale, opts),
    formatDateTime: (
      input: Parameters<typeof formatDateTime>[0],
      opts?: Intl.DateTimeFormatOptions
    ) => formatDateTime(input, formatLocale, opts),
    formatNumber: (value: number, opts?: Intl.NumberFormatOptions) =>
      formatNumber(value, formatLocale, opts),
    formatPercent: (value: number, opts?: FormatPercentOptions) =>
      formatPercent(value, formatLocale, opts),
    formatBytes: (bytes: number) => formatBytes(bytes, formatLocale),
    formatLocale,
  };
}
