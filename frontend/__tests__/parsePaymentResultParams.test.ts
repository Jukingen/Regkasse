import { describe, expect, it } from '@jest/globals';

import {
  isRedirectStatusCancelled,
  isRedirectStatusFailed,
  isRedirectStatusSucceeded,
  parsePaymentResultParams,
} from '../services/payment/parsePaymentResultParams';

describe('parsePaymentResultParams', () => {
  it('reads Stripe query keys payment_intent_client_secret and redirect_status', () => {
    const parsed = parsePaymentResultParams({
      payment_intent: 'pi_abc',
      payment_intent_client_secret: 'pi_abc_secret_xxx',
      redirect_status: 'succeeded',
    });

    expect(parsed.paymentIntentId).toBe('pi_abc');
    expect(parsed.paymentIntentClientSecret).toBe('pi_abc_secret_xxx');
    expect(parsed.redirectStatus).toBe('succeeded');
    expect(isRedirectStatusSucceeded(parsed.redirectStatus)).toBe(true);
  });

  it('maps failed and cancelled redirect_status values', () => {
    expect(isRedirectStatusFailed('failed')).toBe(true);
    expect(isRedirectStatusCancelled('canceled')).toBe(true);
    expect(isRedirectStatusCancelled('cancelled')).toBe(true);
  });
});
