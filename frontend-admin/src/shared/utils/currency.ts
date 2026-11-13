import { formatCurrency } from '@/i18n/formatting';

/** @deprecated Use useCountryFormatting() instead. AT-only shim. */
export function formatEUR(value: number): string {
  return formatCurrency(value, 'de-AT');
}
