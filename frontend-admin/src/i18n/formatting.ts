/**
 * Tek format katmanı: `useI18n().formatLocale` (BCP-47: de-AT, en-US, tr-TR).
 * Metin dili (`textLocale`) ile karıştırma; tarih/sayı/para her zaman `formatLocale` ile.
 *
 * @example
 *   const { formatLocale } = useI18n();
 *   formatCurrency(19.99, formatLocale);
 *
 * @example
 *   const fmt = useMemo(() => createIntlFormatters(formatLocale), [formatLocale]);
 *   fmt.formatNumber(1_200, { maximumFractionDigits: 0 });
 *
 * @example
 *   // Yüzde: Intl `style: 'percent'` — değer 0–1 aralığında (ör. 0,125 → %12,5)
 *   formatPercent(0.125, formatLocale, { maximumFractionDigits: 1 });
 */

export const FORMAT_EMPTY_DISPLAY = '—';

export function formatDateTime(
  input: string | number | Date | null | undefined,
  formatLocale: string,
  options?: Intl.DateTimeFormatOptions,
): string {
  if (input == null || input === '') return FORMAT_EMPTY_DISPLAY;
  const d = input instanceof Date ? input : new Date(input);
  if (Number.isNaN(d.getTime())) return FORMAT_EMPTY_DISPLAY;
  return d.toLocaleString(formatLocale, options);
}

export function formatDate(
  input: string | number | Date | null | undefined,
  formatLocale: string,
  options?: Intl.DateTimeFormatOptions,
): string {
  if (input == null || input === '') return FORMAT_EMPTY_DISPLAY;
  const d = input instanceof Date ? input : new Date(input);
  if (Number.isNaN(d.getTime())) return FORMAT_EMPTY_DISPLAY;
  return d.toLocaleDateString(formatLocale, options);
}

export function formatNumber(value: number, formatLocale: string, options?: Intl.NumberFormatOptions): string {
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
  options?: FormatCurrencyOptions,
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
  options?: FormatPercentOptions,
): string {
  if (!Number.isFinite(value)) return FORMAT_EMPTY_DISPLAY;
  return value.toLocaleString(formatLocale, {
    style: 'percent',
    minimumFractionDigits: options?.minimumFractionDigits ?? 0,
    maximumFractionDigits: options?.maximumFractionDigits ?? 2,
  });
}

export function createIntlFormatters(formatLocale: string) {
  return {
    formatCurrency: (value: number, opts?: FormatCurrencyOptions) => formatCurrency(value, formatLocale, opts),
    formatDate: (input: Parameters<typeof formatDate>[0], opts?: Intl.DateTimeFormatOptions) =>
      formatDate(input, formatLocale, opts),
    formatDateTime: (input: Parameters<typeof formatDateTime>[0], opts?: Intl.DateTimeFormatOptions) =>
      formatDateTime(input, formatLocale, opts),
    formatNumber: (value: number, opts?: Intl.NumberFormatOptions) => formatNumber(value, formatLocale, opts),
    formatPercent: (value: number, opts?: FormatPercentOptions) => formatPercent(value, formatLocale, opts),
    formatLocale,
  };
}
