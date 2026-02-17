/**
 * EUR currency formatter for de-AT locale.
 * Produces output like: € 1.234,56
 */
const eurFormatter = new Intl.NumberFormat('de-AT', {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
});

/**
 * Format a numeric value as EUR in Austrian locale (de-AT).
 * @example formatEUR(1234.5) => "€ 1.234,50"
 */
export function formatEUR(value: number): string {
    return eurFormatter.format(value);
}
