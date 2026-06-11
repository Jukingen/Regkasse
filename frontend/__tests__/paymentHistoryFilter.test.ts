import { describe, expect, it } from '@jest/globals';

import type { PaymentHistoryItem } from '../services/api/paymentHistoryService';
import { filterPaymentHistoryItems } from '../utils/paymentHistoryFilter';

function row(overrides: Partial<PaymentHistoryItem>): PaymentHistoryItem {
  return {
    id: 'id-1',
    receiptNumber: 'R-1',
    totalAmount: 10,
    createdAt: '2026-06-11T10:00:00Z',
    paymentMethod: 'cash',
    customerName: 'Walk-in',
    isStorno: false,
    isRefund: false,
    availableActions: [],
    ...overrides,
  };
}

describe('filterPaymentHistoryItems', () => {
  const payments = [
    row({ id: 'sale', receiptNumber: 'R-100' }),
    row({ id: 'storno', receiptNumber: 'R-101', isStorno: true, totalAmount: -10 }),
    row({ id: 'refund', receiptNumber: 'R-102', isRefund: true, totalAmount: -5 }),
  ];

  it('returns all payments for filter all', () => {
    expect(filterPaymentHistoryItems(payments, 'all')).toHaveLength(3);
  });

  it('returns only storno rows', () => {
    const result = filterPaymentHistoryItems(payments, 'storno');
    expect(result).toHaveLength(1);
    expect(result[0]?.isStorno).toBe(true);
  });

  it('returns only refund rows', () => {
    const result = filterPaymentHistoryItems(payments, 'refund');
    expect(result).toHaveLength(1);
    expect(result[0]?.isRefund).toBe(true);
  });
});
