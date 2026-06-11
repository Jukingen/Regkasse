import type { PaymentHistoryItem } from '../services/api/paymentHistoryService';

export type PaymentHistoryFilterType = 'all' | 'storno' | 'refund';

export function filterPaymentHistoryItems(
  payments: PaymentHistoryItem[],
  filterType: PaymentHistoryFilterType
): PaymentHistoryItem[] {
  if (filterType === 'storno') {
    return payments.filter((p) => p.isStorno);
  }
  if (filterType === 'refund') {
    return payments.filter((p) => p.isRefund);
  }
  return payments;
}
