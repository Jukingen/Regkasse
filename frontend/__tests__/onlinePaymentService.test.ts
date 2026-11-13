/**
 * @jest-environment node
 */
import { describe, expect, it, beforeEach, jest } from '@jest/globals';

import paymentService from '../services/api/paymentService';
import { saveOfflineOrderSnapshot } from '../services/offline/offlineOrderManager';

const mockPost = jest.fn() as jest.MockedFunction<
  (url: string, body?: unknown) => Promise<unknown>
>;
const mockGet = jest.fn() as jest.MockedFunction<(url: string) => Promise<unknown>>;

jest.mock('../services/api/config', () => ({
  apiClient: {
    get: (url: string) => mockGet(url),
    post: (url: string, body?: unknown) => mockPost(url, body),
  },
  API_BASE_URL: 'http://localhost:5184/api',
  resolveTenantFetchRequest: async (url: string, headers: Record<string, string> = {}) => ({
    url,
    headers,
  }),
}));

jest.mock('../services/payment/pendingPaymentQueue', () => {
  const actual = jest.requireActual('../services/payment/pendingPaymentQueue') as Record<
    string,
    unknown
  >;
  return {
    ...actual,
    enqueuePendingPayment: jest.fn(async () => 'pending-queue-id'),
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

jest.mock('../services/offline/offlineOrderManager', () => ({
  saveOfflineOrderSnapshot: jest.fn(async () => ({ id: 'offline-order-1' })),
  syncOfflineOrderSnapshots: jest.fn(async () => ({ success: true, message: '', details: [] })),
}));

jest.mock('../lib/logger', () => ({
  logger: {
    debug: jest.fn(),
    info: jest.fn(),
    warn: jest.fn(),
    error: jest.fn(),
  },
}));

jest.mock('../features/payment/paymentErrors', () => ({
  normalizePaymentError: (err: unknown) => err,
}));

describe('paymentService initiate / status', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('POSTs /pos/payment/initiate and unwraps SuccessResponse data', async () => {
    mockPost.mockResolvedValue({
      success: true,
      data: {
        onlinePaymentId: 'op-abc',
        status: 'AWAITING_PAYMENT_GATEWAY',
        redirectUrl: 'https://gateway.example/pay',
        paymentIntentId: 'pi_1',
      },
    });

    const result = await paymentService.initiateOnlinePayment({
      cashRegisterId: '22222222-2222-2222-2222-222222222222',
      amount: 12.5,
      method: 'paypal',
      idempotencyKey: 'idem-1',
      returnUrl: 'cashregister://online-payment/callback',
    });

    expect(mockPost).toHaveBeenCalledWith(
      '/pos/payment/initiate',
      expect.objectContaining({
        method: 'paypal',
        currency: 'EUR',
        amount: 12.5,
        idempotencyKey: 'idem-1',
      })
    );
    expect(result.onlinePaymentId).toBe('op-abc');
    expect(result.status).toBe('awaiting_action');
    expect(result.redirectUrl).toBe('https://gateway.example/pay');
  });

  it('does not enqueue hosted online payments on transport failure', async () => {
    mockPost.mockRejectedValue({ code: 'ERR_NETWORK', message: 'Network Error' });

    const result = await paymentService.processPayment({
      customerId: '00000000-0000-0000-0000-000000000001',
      items: [
        {
          productId: '11111111-1111-1111-1111-111111111111',
          quantity: 1,
          taxType: 'standard',
        },
      ],
      payment: { method: 'paypal', tseRequired: true, amount: 10 },
      tableNumber: 1,
      totalAmount: 10,
      cashRegisterId: '22222222-2222-2222-2222-222222222222',
      idempotencyKey: 'idem-online-1',
    });

    expect(result.fiscalStatus).toBe('FAILED');
    expect(result.error).toBe('ONLINE_PAYMENT_REQUIRES_ONLINE');
    expect(jest.mocked(saveOfflineOrderSnapshot)).not.toHaveBeenCalled();
  });

  it('polls GET /pos/payment/initiate/{id} until completed', async () => {
    mockGet
      .mockResolvedValueOnce({ data: { id: 'op-abc', status: 'processing' } })
      .mockResolvedValueOnce({ data: { id: 'op-abc', status: 'completed', paymentId: 'pay-1' } });

    const result = await paymentService.pollOnlinePaymentStatus('op-abc', {
      intervalMs: 1,
      timeoutMs: 5000,
    });

    expect(mockGet).toHaveBeenCalledWith('/pos/payment/initiate/op-abc');
    expect(result.status).toBe('completed');
    expect(result.paymentId).toBe('pay-1');
  });
});
