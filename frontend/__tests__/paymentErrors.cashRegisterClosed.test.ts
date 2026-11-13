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

describe('paymentErrors LIMIT_EXCEEDED', () => {
  it('maps classic 409 LimitErrorDto to German cashier copy', () => {
    const err = normalizePaymentError({
      response: {
        status: 409,
        data: {
          code: 'LIMIT_EXCEEDED',
          limitKey: 'dailyMaxTransactions',
          limit: 1000,
          current: 1000,
          message: 'Daily transaction limit of 1000 reached',
          canForce: false,
        },
      },
    });
    expect(err.code).toBe('LIMIT_EXCEEDED');
    expect(err.limitKey).toBe('dailyMaxTransactions');
    expect(getPaymentErrorDisplayMessage(err)).toMatch(/Tägliches Transaktionslimit|Daily transaction limit/i);
  });

  it('maps nested v2 limitError + offline queue key', () => {
    const err = normalizePaymentError({
      response: {
        status: 409,
        data: {
          code: 'LIMIT_EXCEEDED',
          message: 'Offline queue limit of 50 reached',
          limitError: {
            code: 'LIMIT_EXCEEDED',
            limitKey: 'maxOfflineTransactions',
            limit: 50,
            current: 50,
            canForce: false,
          },
        },
      },
    });
    expect(err.code).toBe('LIMIT_EXCEEDED');
    expect(getPaymentErrorDisplayMessage(err)).toMatch(/Offline-Warteschlange voll|Offline queue is full/i);
    expect(
      getPaymentResponseFailureMessage({
        code: 'LIMIT_EXCEEDED',
        diagnosticCode: 'LIMIT_EXCEEDED',
        limitKey: 'maxTransactionAmount',
        limit: 100,
        current: 250,
      })
    ).toMatch(/Maximaler Transaktionsbetrag|Maximum transaction amount/i);
  });
});
