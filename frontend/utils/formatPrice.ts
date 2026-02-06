/**
 * Format price in Austrian EUR format (de-AT)
 * Examples:
 * - 16.50 → "€ 16,50"
 * - 1234.56 → "€ 1.234,56"
 * - null → "€ 0,00"
 */
export const formatPrice = (amount: number | null | undefined): string => {
    if (amount === null || amount === undefined || isNaN(amount)) {
        return '€ 0,00'; // Safe fallback
    }

    return new Intl.NumberFormat('de-AT', {
        style: 'currency',
        currency: 'EUR',
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    }).format(amount);
};

/**
 * Format percentage (e.g., tax rate)
 * Examples:
 * - 0.20 → "20%"
 * - 0.13 → "13%"
 */
export const formatPercent = (rate: number): string => {
    return `${(rate * 100).toFixed(0)}%`;
};
