import { describe, expect, it } from '@jest/globals';

import {
  getPaymentErrorDisplayMessage,
  getPaymentErrorMessage,
  getPaymentResponseFailureMessage,
  normalizePaymentError,
  PaymentAppError,
} from '../features/payment/paymentErrors';
import { POS_CASH_REGISTER_CODES } from '../utils/posRegisterGateCopy';

describe('paymentErrors cash register closed', () => {
  it('maps axios 400 CASH_REGISTER_CLOSED to German via normalizePaymentError + display helper', () => {
    const axiosLike = {
      response: {
        status: 400,
        data: {
          success: false,
          message: 'Cash register is closed or not usable for payment.',
          details: {
            code: POS_CASH_REGISTER_CODES.CLOSED,
            diagnosticCode: POS_CASH_REGISTER_CODES.CLOSED,
          },
        },
      },
    };
    const err = normalizePaymentError(axiosLike);
    expect(err).toBeInstanceOf(PaymentAppError);
    expect(err.diagnosticCode).toBe(POS_CASH_REGISTER_CODES.CLOSED);
    expect(getPaymentErrorDisplayMessage(err)).toMatch(/nicht geöffnet/i);
  });

  it('maps success:false payment body with diagnosticCode to German', () => {
    const msg = getPaymentResponseFailureMessage({
      fiscalStatus: 'FAILED',
      message: 'Cash register is closed or not usable for payment.',
      diagnosticCode: POS_CASH_REGISTER_CODES.CLOSED,
    });
    expect(msg).toMatch(/nicht geöffnet/i);
  });
});

describe('paymentErrors Fiskaly / license / Monatsbeleg', () => {
  it('maps TSE_UNAVAILABLE to German cashier copy', () => {
    const err = normalizePaymentError({
      response: {
        status: 400,
        data: {
          success: false,
          message: 'TSE is not available',
          details: { diagnosticCode: 'TSE_UNAVAILABLE' },
        },
      },
    });
    expect(err.code).toBe('TSE_UNAVAILABLE');
    expect(getPaymentErrorDisplayMessage(err)).toBe(getPaymentErrorMessage('TSE_UNAVAILABLE'));
    expect(
      getPaymentResponseFailureMessage({
        fiscalStatus: 'FAILED',
        message: 'Failed to generate TSE signature',
        diagnosticCode: 'TSE_UNAVAILABLE',
      })
    ).toBe(getPaymentErrorMessage('TSE_UNAVAILABLE'));
  });

  it('maps LICENSE_LOCKED to German cashier copy', () => {
    const err = normalizePaymentError({
      response: {
        status: 403,
        data: { code: 'LICENSE_LOCKED', message: 'Mandant license lockdown' },
      },
    });
    expect(err.code).toBe('LICENSE_LOCKED');
    expect(getPaymentErrorDisplayMessage(err)).toBe(getPaymentErrorMessage('LICENSE_LOCKED'));
  });

  it('maps CASH_REGISTER_MONATSBELEG_REQUIRED to short German copy', () => {
    const err = normalizePaymentError({
      response: {
        status: 400,
        data: {
          details: { diagnosticCode: POS_CASH_REGISTER_CODES.MONATSBELEG_REQUIRED },
        },
      },
    });
    expect(err.code).toBe('MONATSBELEG_REQUIRED');
    expect(getPaymentErrorDisplayMessage(err)).toBe(getPaymentErrorMessage('MONATSBELEG_REQUIRED'));
  });
});
