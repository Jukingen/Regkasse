import { describe, expect, it, jest } from '@jest/globals';

jest.mock('../services/api/config', () => ({
  apiClient: {
    get: jest.fn(),
    post: jest.fn(),
  },
  API_BASE_URL: 'http://test/api',
  resolveTenantFetchHeaders: jest.fn(async (headers: Record<string, string>) => headers),
}));

import {
  parsePaymentHistoryItem,
  parsePaymentHistoryResponse,
  parseStornoResponse,
  paymentHistoryLabelKeyToI18n,
} from '../services/api/paymentHistoryService';

describe('paymentHistoryService parsers', () => {
  it('parsePaymentHistoryItem reads camelCase payment row', () => {
    const item = parsePaymentHistoryItem({
      id: 'pay-1',
      receiptNumber: 'R-100',
      totalAmount: 12.5,
      createdAt: '2026-06-11T10:00:00Z',
      paymentMethod: 'cash',
      customerName: 'Anna',
      tableNumber: 4,
      isStorno: false,
      isRefund: false,
      availableActions: [
        {
          action: 'storno',
          labelKey: 'paymentHistory.actions.storno',
          requiresReason: true,
          requiresManagerApproval: false,
          reasonLabelKey: 'paymentHistory.reasons.stornoTitle',
          reasonOptions: [
            { code: 'CUSTOMER_REQUEST', labelKey: 'paymentHistory.reasons.customerRequest' },
          ],
        },
      ],
    });
    expect(item?.id).toBe('pay-1');
    expect(item?.totalAmount).toBe(12.5);
    expect(item?.tableNumber).toBe(4);
    expect(item?.availableActions[0]?.action).toBe('storno');
    expect(item?.availableActions[0]?.reasonOptions[0]?.code).toBe('CUSTOMER_REQUEST');
  });

  it('parsePaymentHistoryItem returns null without id', () => {
    expect(parsePaymentHistoryItem({ receiptNumber: 'R-1' })).toBeNull();
  });

  it('parsePaymentHistoryResponse unwraps success envelope', () => {
    const res = parsePaymentHistoryResponse({
      success: true,
      data: {
        payments: [
          {
            id: 'pay-2',
            receiptNumber: 'R-200',
            totalAmount: 5,
            createdAt: '2026-06-11T11:00:00Z',
            paymentMethod: 'card',
            customerName: 'Walk-in',
            isStorno: false,
            isRefund: false,
            availableActions: [],
          },
        ],
        totalCount: 1,
        limit: 20,
        offset: 0,
        hasMore: false,
        fromUtc: '2026-06-10T11:00:00Z',
        toUtc: '2026-06-11T11:00:00Z',
        cashRegisterId: 'reg-1',
        language: 'de',
      },
    });
    expect(res.payments).toHaveLength(1);
    expect(res.totalCount).toBe(1);
    expect(res.cashRegisterId).toBe('reg-1');
    expect(res.language).toBe('de');
  });

  it('parseStornoResponse maps approval fields', () => {
    const res = parseStornoResponse({
      success: false,
      errorKey: 'errors.approvalRequired',
      requiresApproval: true,
      approvalRequestId: 'apr-1',
    });
    expect(res.success).toBe(false);
    expect(res.errorKey).toBe('errors.approvalRequired');
    expect(res.requiresApproval).toBe(true);
    expect(res.approvalRequestId).toBe('apr-1');
  });

  it('paymentHistoryLabelKeyToI18n maps dotted backend keys', () => {
    expect(paymentHistoryLabelKeyToI18n('paymentHistory.actions.storno')).toBe(
      'paymentHistory:actions.storno'
    );
    expect(paymentHistoryLabelKeyToI18n('errors.paymentNotFound')).toBe(
      'paymentHistory:errors.paymentNotFound'
    );
    expect(paymentHistoryLabelKeyToI18n('messages.stornoSuccess')).toBe(
      'paymentHistory:messages.stornoSuccess'
    );
  });
});
