import { describe, expect, it } from '@jest/globals';

import { redactLogMeta } from '../lib/logger';
import {
  isHostedOnlinePaymentMethod,
  isOnlinePaymentDisabledOffline,
  mergeHostedOnlinePaymentMethods,
} from '../services/payment/onlinePaymentMethods';
import type { PaymentMethod } from '../services/api/paymentService';

describe('hosted online payment methods', () => {
  it('recognizes Kreditkarte and PayPal codes only', () => {
    expect(isHostedOnlinePaymentMethod('credit_card')).toBe(true);
    expect(isHostedOnlinePaymentMethod('paypal')).toBe(true);
    expect(isHostedOnlinePaymentMethod('card')).toBe(false);
    expect(isHostedOnlinePaymentMethod('cash')).toBe(false);
  });

  it('disables hosted methods when the device is offline', () => {
    expect(isOnlinePaymentDisabledOffline('paypal', false)).toBe(true);
    expect(isOnlinePaymentDisabledOffline('credit_card', true)).toBe(false);
    expect(isOnlinePaymentDisabledOffline('cash', false)).toBe(false);
  });

  it('appends Kreditkarte and PayPal when the catalog omits them', () => {
    const catalog: PaymentMethod[] = [
      { id: 'cash', name: 'Bar', type: 'cash', icon: 'cash-outline' },
    ];
    const merged = mergeHostedOnlinePaymentMethods(catalog);
    expect(merged.map((m) => m.type)).toEqual(['cash', 'credit_card', 'paypal']);
    expect(merged.find((m) => m.type === 'credit_card')?.name).toBe('Kreditkarte');
    expect(merged.find((m) => m.type === 'paypal')?.name).toBe('PayPal');
  });

  it('keeps catalog names when codes already exist', () => {
    const catalog: PaymentMethod[] = [
      { id: 'pp', name: 'PayPal Business', type: 'paypal', icon: 'logo-paypal' },
    ];
    const merged = mergeHostedOnlinePaymentMethods(catalog);
    expect(merged.filter((m) => m.type === 'paypal')).toHaveLength(1);
    expect(merged.find((m) => m.type === 'paypal')?.name).toBe('PayPal Business');
  });
});

describe('logger redaction', () => {
  it('redacts tokens, voucher codes, and PAN-like keys', () => {
    const redacted = redactLogMeta({
      onlinePaymentId: 'op-1',
      accessToken: 'super-secret',
      voucherCode: 'GUT-SECRET',
      cardNumber: '4242424242424242',
      nested: { clientSecret: 'sk_live_xxx' },
    });
    expect(redacted?.onlinePaymentId).toBe('op-1');
    expect(redacted?.accessToken).toBe('[redacted]');
    expect(redacted?.voucherCode).toBe('[redacted]');
    expect(redacted?.cardNumber).toBe('[redacted]');
    expect((redacted?.nested as { clientSecret: string }).clientSecret).toBe('[redacted]');
  });
});
