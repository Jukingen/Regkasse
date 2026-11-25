/**
 * Refund on a voucher-paid sale is refused by the backend with VOUCHER_PARTIAL_REFUND_NOT_SUPPORTED.
 * paymentService.refundPayment must keep that code (instead of the generic "needs server connection" text).
 */
import { jest, describe, it, expect, beforeEach } from '@jest/globals';

import paymentService, {
  PaymentRefundRejectedError,
  readRefundDiagnosticCode,
  VOUCHER_PARTIAL_REFUND_NOT_SUPPORTED_CODE,
} from '../services/api/paymentService';

const mockPost = jest.fn() as jest.MockedFunction<
  (url: string, body?: unknown) => Promise<unknown>
>;

jest.mock('../services/api/config', () => ({
  apiClient: {
    get: jest.fn(),
    post: (url: string, body?: unknown) => mockPost(url, body),
  },
  API_BASE_URL: 'http://localhost',
  resolveTenantFetchRequest: async (url: string, headers: Record<string, string> = {}) => ({
    url,
    headers,
  }),
}));

function posRefundError(diagnosticCode?: string) {
  return {
    response: {
      status: 400,
      data: {
        success: false,
        message: 'Voucher-paid sales support full storno only; a refund is not supported.',
        details: diagnosticCode ? { diagnosticCode } : undefined,
      },
    },
  };
}

describe('paymentService.refundPayment voucher refusal', () => {
  beforeEach(() => {
    mockPost.mockReset();
    jest.spyOn(console, 'error').mockImplementation(() => undefined);
  });

  it('reads details.diagnosticCode from the POS error body', () => {
    expect(readRefundDiagnosticCode(posRefundError('SOME_CODE'))).toBe('SOME_CODE');
    expect(readRefundDiagnosticCode(posRefundError())).toBeUndefined();
    expect(readRefundDiagnosticCode(new Error('network'))).toBeUndefined();
  });

  it('rethrows the voucher refusal with its diagnostic code', async () => {
    mockPost.mockRejectedValueOnce(posRefundError(VOUCHER_PARTIAL_REFUND_NOT_SUPPORTED_CODE));

    const error = await paymentService.refundPayment('p1', 2.5, 'Reklamation').catch((e) => e);

    expect(error).toBeInstanceOf(PaymentRefundRejectedError);
    expect((error as PaymentRefundRejectedError).diagnosticCode).toBe(
      'VOUCHER_PARTIAL_REFUND_NOT_SUPPORTED'
    );
  });

  it('keeps the generic server-connection message for other failures', async () => {
    mockPost.mockRejectedValueOnce(posRefundError('REFUND_EXCEEDS_REMAINING'));

    const error = await paymentService.refundPayment('p1', 2.5, 'Reklamation').catch((e) => e);

    expect(error).not.toBeInstanceOf(PaymentRefundRejectedError);
    expect((error as Error).message).toContain('Erstattung erfordert Serververbindung');
  });
});
