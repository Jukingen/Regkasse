/**
 * Formatting locale helpers for admin (BCP-47: de-AT, en-US, tr-TR).
 * Use `formatLocale` from `useI18n()` — separate from translation text locale (de | en | tr).
 */

export function formatDateTime(
  input: string | number | Date | null | undefined,
  formatLocale: string,
): string {
  if (input == null || input === '') return '—';
  const d = input instanceof Date ? input : new Date(input);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString(formatLocale);
}

export function formatDate(
  input: string | number | Date | null | undefined,
  formatLocale: string,
  options?: Intl.DateTimeFormatOptions,
): string {
  if (input == null || input === '') return '—';
  const d = input instanceof Date ? input : new Date(input);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleDateString(formatLocale, options);
}

export function formatNumber(value: number, formatLocale: string, options?: Intl.NumberFormatOptions): string {
  return value.toLocaleString(formatLocale, options);
}
