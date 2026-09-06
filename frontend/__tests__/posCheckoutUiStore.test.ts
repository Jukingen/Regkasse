import { describe, expect, it } from '@jest/globals';

import {
  DEFAULT_POS_PAYMENT_METHOD,
  usePosCheckoutUiStore,
} from '../stores/posCheckoutUiStore';

describe('posCheckoutUiStore', () => {
  it('defaults to Bar (cash) and restores cash on reset', () => {
    expect(DEFAULT_POS_PAYMENT_METHOD).toBe('cash');
    usePosCheckoutUiStore.getState().setSelectedPaymentMethodType('card');
    expect(usePosCheckoutUiStore.getState().selectedPaymentMethodType).toBe('card');
    usePosCheckoutUiStore.getState().resetCheckoutPaymentUi();
    expect(usePosCheckoutUiStore.getState().selectedPaymentMethodType).toBe('cash');
  });
});
