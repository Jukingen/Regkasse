/**
 * Ensures paymentService.processPayment normalizes item.taxType before HTTP and before offline enqueue.
 */
import { jest, describe, it, expect, beforeEach } from '@jest/globals';
import type { PaymentRequest } from '../services/api/paymentService';

const mockPost = jest.fn() as jest.MockedFunction<(url: string, body?: unknown) => Promise<unknown>>;
const mockEnqueuePendingPayment = jest.fn() as jest.MockedFunction<(payload: unknown) => Promise<string>>;
mockEnqueuePendingPayment.mockResolvedValue('pending-queue-id');

jest.mock('../services/api/config', () => ({
  apiClient: {
    get: jest.fn(),
    post: (url: string, body?: unknown) => mockPost(url, body),
  },
  API_BASE_URL: 'http://localhost',
}));

jest.mock('../services/payment/pendingPaymentQueue', () => {
  const actual = jest.requireActual(
    '../services/payment/pendingPaymentQueue'
  ) as typeof import('../services/payment/pendingPaymentQueue');
  return {
    ...actual,
    enqueuePendingPayment: (payload: unknown) => mockEnqueuePendingPayment(payload),
    syncPendingPaymentQueue: jest.fn(async () => ({ processed: 0, failed: 0 })),
    removePendingByIdempotencyKey: jest.fn(async () => undefined),
    getPendingPaymentQueue: jest.fn(async () => []),
  };
});

jest.mock('../utils/storage', () => ({
  storage: {
    getItem: jest.fn(async () => null),
    setItem: jest.fn(),
    removeItem: jest.fn(),
    multiRemove: jest.fn(),
  },
}));

jest.mock('../features/payment/paymentErrors', () => ({
  normalizePaymentError: (err: unknown) => err,
}));

import paymentService from '../services/api/paymentService';
import { paymentPayloadContainsVoucherSecrets } from '../services/payment/pendingPaymentQueue';

function baseRequest(overrides: Partial<PaymentRequest> = {}): PaymentRequest {
  return {
    customerId: '00000000-0000-0000-0000-000000000001',
    items: [
      {
        productId: '11111111-1111-1111-1111-111111111111',
        quantity: 1,
        taxType: 'standard',
      },
    ],
    payment: { method: 'cash', tseRequired: false, amount: 10 },
    tableNumber: 1,
    totalAmount: 10,
    cashRegisterId: '22222222-2222-2222-2222-222222222222',
    idempotencyKey: 'test-key-1',
    ...overrides,
  };
}

describe('paymentService.processPayment taxType normalization', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('normalizes numeric taxType on items before apiClient.post', async () => {
    mockPost.mockResolvedValue({
      success: true,
      paymentId: 'pay-ok',
    });

    const raw = baseRequest({
      items: [
        {
          productId: '11111111-1111-1111-1111-111111111111',
          quantity: 2,
          taxType: 2 as unknown as PaymentRequest['items'][0]['taxType'],
        },
      ],
    });

    await paymentService.processPayment(raw);

    expect(mockPost).toHaveBeenCalledTimes(1);
    const postedBody = mockPost.mock.calls[0][1] as PaymentRequest;
    expect(postedBody.items[0].taxType).toBe('reduced');
  });

  it('preserves legacy string taxType and still passes through normalizer', async () => {
    mockPost.mockResolvedValue({ success: true, paymentId: 'pay-2' });

    await paymentService.processPayment(
      baseRequest({
        items: [
          {
            productId: '11111111-1111-1111-1111-111111111111',
            quantity: 1,
            taxType: 'reduced',
          },
        ],
      })
    );

    const postedBody = mockPost.mock.calls[0][1] as PaymentRequest;
    expect(postedBody.items[0].taxType).toBe('reduced');
  });

  it('stores normalized items on offline enqueue path', async () => {
    mockPost.mockRejectedValue({
      response: undefined,
      message: 'Network Error',
      code: 'ERR_NETWORK',
    });

    const raw = baseRequest({
      idempotencyKey: 'offline-key',
      items: [
        {
          productId: '11111111-1111-1111-1111-111111111111',
          quantity: 1,
          taxType: 3 as unknown as PaymentRequest['items'][0]['taxType'],
        },
      ],
    });

    const res = await paymentService.processPayment(raw);

    expect(res.fiscalStatus).toBe('NON_FISCAL_PENDING');
    expect(mockEnqueuePendingPayment).toHaveBeenCalledTimes(1);
    const enqueued = mockEnqueuePendingPayment.mock.calls[0][0] as { items: { taxType: string }[] };
    expect(enqueued.items[0].taxType).toBe('special');
  });

  it('does not enqueue voucher payments on transport failure (no plaintext in offline queue)', async () => {
    mockPost.mockRejectedValue({
      response: undefined,
      message: 'Network Error',
      code: 'ERR_NETWORK',
    });

    const raw = baseRequest({
      idempotencyKey: 'voucher-offline-key',
      payment: {
        method: 'voucher',
        tseRequired: false,
        voucherCode: 'GUT-TEST-SECRET',
      },
    });

    const res = await paymentService.processPayment(raw);

    expect(res.fiscalStatus).toBe('FAILED');
    expect(res.success).toBe(false);
    expect(mockEnqueuePendingPayment).not.toHaveBeenCalled();
    expect(res.error).toBe('VOUCHER_REQUIRES_ONLINE');
    expect(res.message).toMatch(/Gutschein/i);
  });
});

describe('paymentPayloadContainsVoucherSecrets', () => {
  it('detects non-empty voucherCode', () => {
    expect(
      paymentPayloadContainsVoucherSecrets({
        method: 'voucher',
        tseRequired: false,
        voucherCode: 'GUT-1',
      })
    ).toBe(true);
  });

  it('detects voucherRedemptions with codes', () => {
    expect(
      paymentPayloadContainsVoucherSecrets({
        method: 'voucher',
        tseRequired: false,
        voucherRedemptions: [{ code: 'X', amount: 1 }],
      })
    ).toBe(true);
  });

  it('is false for cash', () => {
    expect(paymentPayloadContainsVoucherSecrets({ method: 'cash', tseRequired: false, amount: 1 })).toBe(
      false
    );
  });
});
