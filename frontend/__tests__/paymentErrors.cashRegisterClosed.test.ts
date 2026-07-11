import { describe, expect, it } from '@jest/globals';

import {
  getPaymentErrorDisplayMessage,
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
