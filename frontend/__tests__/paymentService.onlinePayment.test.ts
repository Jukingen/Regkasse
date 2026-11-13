/**
 * Unit tests for hosted online-payment helpers on paymentService.
 */
import { jest, describe, it, expect, beforeEach } from '@jest/globals';

import paymentService, { resolveGatewayChargeAmount } from '../services/api/paymentService';

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
  resolveTenantFetchHeaders: () => ({}),
  resolveTenantFetchRequest: async (url: string, headers: Record<string, string> = {}) => ({
    url,
    headers,
  }),
}));

jest.mock('../utils/storage', () => ({
  storage: {
    getItem: jest.fn(async () => null),
    setItem: jest.fn(),
    removeItem: jest.fn(),
    multiRemove: jest.fn(),
  },
}));

jest.mock('../lib/logger', () => ({
  logger: {
    debug: jest.fn(),
    info: jest.fn(),
    warn: jest.fn(),
    error: jest.fn(),
  },
}));

jest.mock('../services/offline/offlineOrderManager', () => ({
  saveOfflineOrderSnapshot: jest.fn(async () => ({ id: 'offline-order-1' })),
  syncOfflineOrderSnapshots: jest.fn(async () => ({ success: true, message: '', details: [] })),
}));

const INITIATE_BODY = {
  cashRegisterId: '11111111-1111-4111-8111-111111111111',
  amount: 12.5,
  method: 'paypal',
  idempotencyKey: 'idem-1',
};

describe('paymentService online payment helpers', () => {
  beforeEach(() => {
    mockPost.mockReset();
    mockGet.mockReset();
  });

  it('initiateOnlinePayment posts to /pos/payment/initiate and parses the envelope', async () => {
    mockPost.mockResolvedValue({
      data: {
        onlinePaymentId: 'op-abc',
        paymentIntentId: 'pi_abc',
        status: 'awaiting_payment_gateway',
        redirectUrl: 'https://gateway.test/pay',
        provider: 'Mock',
      },
    });

    const result = await paymentService.initiateOnlinePayment(INITIATE_BODY);

    expect(mockPost).toHaveBeenCalledWith(
      '/pos/payment/initiate',
      expect.objectContaining({
        cashRegisterId: INITIATE_BODY.cashRegisterId,
        amount: 12.5,
        currency: 'EUR',
        method: 'paypal',
        idempotencyKey: 'idem-1',
      })
    );
    expect(result.onlinePaymentId).toBe('op-abc');
    expect(result.paymentIntentId).toBe('pi_abc');
    expect(result.status).toBe('awaiting_action');
    expect(result.redirectUrl).toBe('https://gateway.test/pay');
    expect(result.provider).toBe('Mock');
  });

  it('initiateOnlinePayment throws when the gateway id is missing', async () => {
    mockPost.mockResolvedValue({ data: { status: 'pending' } });
    await expect(paymentService.initiateOnlinePayment(INITIATE_BODY)).rejects.toThrow(
      /Online-Zahlung konnte nicht gestartet werden/
    );
  });

  it('getOnlinePaymentStatus polls GET /pos/payment/initiate/{id}', async () => {
    mockGet.mockResolvedValue({
      onlinePaymentId: 'op-abc',
      status: 'succeeded',
      paymentDetailsId: 'pd-1',
    });

    const result = await paymentService.getOnlinePaymentStatus('op-abc');
    expect(mockGet).toHaveBeenCalledWith('/pos/payment/initiate/op-abc');
    expect(result.status).toBe('completed');
    expect(result.paymentDetailsId).toBe('pd-1');
  });

  it('pollOnlinePaymentStatus returns on the first terminal status', async () => {
    mockGet.mockResolvedValueOnce({ status: 'processing', onlinePaymentId: 'op-abc' });
    mockGet.mockResolvedValueOnce({
      status: 'completed',
      onlinePaymentId: 'op-abc',
      paymentDetailsId: 'pd-9',
    });

    const seen: string[] = [];
    const result = await paymentService.pollOnlinePaymentStatus('op-abc', {
      intervalMs: 1,
      timeoutMs: 5_000,
      onStatus: (s) => seen.push(s.status),
    });

    expect(result.status).toBe('completed');
    expect(result.paymentDetailsId).toBe('pd-9');
    expect(seen).toEqual(['processing', 'completed']);
  });

  it('pollOnlinePaymentStatus treats GATEWAY_SUCCEEDED as terminal (POS then commits fiscal)', async () => {
    mockGet.mockResolvedValueOnce({
      status: 'GATEWAY_SUCCEEDED',
      onlinePaymentId: 'op-abc',
    });

    const result = await paymentService.pollOnlinePaymentStatus('op-abc', {
      intervalMs: 1,
      timeoutMs: 5_000,
    });

    expect(result.status).toBe('completed');
    expect(result.onlinePaymentId).toBe('op-abc');
  });

  it('resolveGatewayChargeAmount uses remainder after voucher, not cart gross', () => {
    expect(resolveGatewayChargeAmount({ cartTotal: 10, remainderAfterVoucher: 4 })).toBe(4);
    expect(resolveGatewayChargeAmount({ cartTotal: 10, remainderAfterVoucher: 0 })).toBe(0);
    expect(resolveGatewayChargeAmount({ cartTotal: 10, remainderAfterVoucher: undefined })).toBe(10);
  });
});
