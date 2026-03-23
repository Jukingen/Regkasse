import { DEFAULT_FORMAT_LOCALE } from '../i18n/localeUtils';

/**
 * Format price in EUR using an Intl locale (default: Austrian formatting for POS).
 * Pass `intlLocale` from `getFormattingLocaleForTextLocale(i18n.language)` when UI language differs.
 */
export function formatPrice(
  amount: number | null | undefined,
  intlLocale: string = DEFAULT_FORMAT_LOCALE
): string {
  if (amount === null || amount === undefined || isNaN(amount)) {
    return new Intl.NumberFormat(intlLocale, {
      style: 'currency',
      currency: 'EUR',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(0);
  }

  return new Intl.NumberFormat(intlLocale, {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(amount);
}

/**
 * Format percentage (e.g., tax rate)
 * Examples:
 * - 0.20 → "20%"
 * - 0.13 → "13%"
 */
export const formatPercent = (rate: number): string => {
    return `${(rate * 100).toFixed(0)}%`;
};
