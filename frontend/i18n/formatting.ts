/**
 * Date/number formatting helpers using BCP-47 formatting locales (de-AT, en-US, tr-TR).
 * Pass `formatLocale` from `getFormattingLocaleForTextLocale(i18n.language)` so UI text and Intl stay aligned.
 */

export function formatDateTime(
  input: string | number | Date | null | undefined,
  formatLocale: string,
): string {
  if (input == null || input === '') return '';
  const d = input instanceof Date ? input : new Date(input);
  if (Number.isNaN(d.getTime())) return '';
  return d.toLocaleString(formatLocale);
}

export function formatDate(
  input: string | number | Date | null | undefined,
  formatLocale: string,
  options?: Intl.DateTimeFormatOptions,
): string {
  if (input == null || input === '') return '';
  const d = input instanceof Date ? input : new Date(input);
  if (Number.isNaN(d.getTime())) return '';
  return d.toLocaleDateString(formatLocale, options);
}

export function formatTime(
  input: string | number | Date | null | undefined,
  formatLocale: string,
  options?: Intl.DateTimeFormatOptions,
): string {
  if (input == null || input === '') return '';
  const d = input instanceof Date ? input : new Date(input);
  if (Number.isNaN(d.getTime())) return '';
  return d.toLocaleTimeString(formatLocale, options);
}

export function formatNumber(value: number, formatLocale: string, options?: Intl.NumberFormatOptions): string {
  return new Intl.NumberFormat(formatLocale, options).format(value);
}
