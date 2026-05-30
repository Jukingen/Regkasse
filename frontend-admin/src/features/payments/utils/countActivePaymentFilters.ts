import type { PaymentFilters } from '@/features/payments/types/paymentFilters';

/** Counts distinct filter dimensions (not individual multi-select values). */
export function countActivePaymentFilters(filters: PaymentFilters): number {
    let count = 0;
    if (filters.dateRange?.[0] && filters.dateRange[1]) count++;
    if (filters.paymentMethods && filters.paymentMethods.length > 0) count++;
    if (filters.statuses && filters.statuses.length > 0) count++;
    if (filters.receiptNumber?.trim()) count++;
    if (filters.minAmount != null && Number.isFinite(filters.minAmount)) count++;
    if (filters.maxAmount != null && Number.isFinite(filters.maxAmount)) count++;
    if (filters.cashRegisterId) count++;
    if (filters.customerName?.trim()) count++;
    if (filters.customerEmail?.trim()) count++;
    if (filters.cashierId) count++;
    if (filters.isStorno === true) count++;
    if (filters.isRefund === true) count++;
    return count;
}
