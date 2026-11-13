/**
 * POS ↔ backend contract: hosted online payment uses `/api/pos/payment/initiate`
 * (axios baseURL already includes `/api`) with the documented payload.
 * Must never call `/api/admin/*`.
 */
import { jest, describe, it, expect, beforeEach } from '@jest/globals';

import { buildLoginPayload } from '../../services/api/loginPayload';
import paymentService from '../../services/api/paymentService';
import {
  POS_PAYMENT_API_PREFIX,
  POS_PAYMENT_INITIATE_PATH,
  posPaymentInitiatePath,
  posPaymentInitiateStatusPath,
} from '../../services/api/posPaymentPaths';

const mockPost = jest.fn() as jest.MockedFunction<
  (url: string, body?: unknown) => Promise<unknown>
>;
const mockGet = jest.fn() as jest.MockedFunction<(url: string) => Promise<unknown>>;

jest.mock('../../services/api/config', () => ({
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

jest.mock('../../utils/storage', () => ({
  storage: {
    getItem: jest.fn(async () => null),
    setItem: jest.fn(),
    removeItem: jest.fn(),
    multiRemove: jest.fn(),
  },
}));

jest.mock('../../lib/logger', () => ({
  logger: {
    debug: jest.fn(),
    info: jest.fn(),
    warn: jest.fn(),
    error: jest.fn(),
  },
}));

jest.mock('../../services/offline/offlineOrderManager', () => ({
  saveOfflineOrderSnapshot: jest.fn(async () => ({ id: 'offline-order-1' })),
  syncOfflineOrderSnapshots: jest.fn(async () => ({ success: true, message: '', details: [] })),
}));

describe('POS online payment API contract', () => {
  beforeEach(() => {
    mockPost.mockReset();
    mockGet.mockReset();
  });

  it('keeps initiate paths on the canonical POS payment prefix', () => {
    expect(POS_PAYMENT_API_PREFIX).toBe('/pos/payment');
    expect(POS_PAYMENT_INITIATE_PATH).toBe('/pos/payment/initiate');
    expect(posPaymentInitiatePath()).toBe('/pos/payment/initiate');
    expect(posPaymentInitiateStatusPath('op-1')).toBe('/pos/payment/initiate/op-1');
    expect(posPaymentInitiateStatusPath('a/b')).toBe('/pos/payment/initiate/a%2Fb');
  });

  it('authenticates POS sessions with loginIdentifier and clientApp: pos', () => {
    const payload = buildLoginPayload('cashier1', 'Secret123!', 'pos');
    expect(payload).toEqual({
      loginIdentifier: 'cashier1',
      email: 'cashier1',
      password: 'Secret123!',
      clientApp: 'pos',
    });
  });

  it('POSTs initiate with cashRegisterId, amount, method, and idempotencyKey', async () => {
    mockPost.mockResolvedValue({
      onlinePaymentId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      status: 'pending',
      redirectUrl: 'https://pay.test/r',
    });

    await paymentService.initiateOnlinePayment({
      cashRegisterId: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
      amount: 9.99,
      method: 'credit_card',
      idempotencyKey: 'key-42',
      currency: 'EUR',
      customerId: '00000000-0000-0000-0000-000000000001',
      tableNumber: 3,
      returnUrl: 'https://pos.regkasse.at/pay/return',
      cancelUrl: 'https://pos.regkasse.at/pay/cancel',
    });

    expect(mockPost).toHaveBeenCalledTimes(1);
    const [url, body] = mockPost.mock.calls[0] as [string, Record<string, unknown>];
    expect(url).toBe('/pos/payment/initiate');
    expect(url).not.toMatch(/\/admin\//);
    expect(body).toEqual({
      cashRegisterId: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
      amount: 9.99,
      currency: 'EUR',
      method: 'credit_card',
      idempotencyKey: 'key-42',
      customerId: '00000000-0000-0000-0000-000000000001',
      tableNumber: 3,
      returnUrl: 'https://pos.regkasse.at/pay/return',
      cancelUrl: 'https://pos.regkasse.at/pay/cancel',
    });
  });

  it('GETs status from /pos/payment/initiate/{id} and never hits admin routes', async () => {
    mockGet.mockResolvedValue({
      onlinePaymentId: 'op-9',
      status: 'processing',
    });

    await paymentService.getOnlinePaymentStatus('op-9');

    expect(mockGet).toHaveBeenCalledWith('/pos/payment/initiate/op-9');
    const url = String(mockGet.mock.calls[0]?.[0] ?? '');
    expect(url.startsWith('/pos/payment/')).toBe(true);
    expect(url).not.toMatch(/\/admin\//);
    expect(url).not.toMatch(/\/api\/Payment/i);
  });
});
