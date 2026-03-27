import { formatCurrency } from '@/i18n/formatting';

/**
 * Sabit `de-AT` EUR formatı (önceki davranış).
 * @deprecated Yeni kodda `formatCurrency(value, useI18n().formatLocale)` veya `createIntlFormatters` kullanın.
 */
export function formatEUR(value: number): string {
    return formatCurrency(value, 'de-AT');
}
