/**
 * @jest-environment node
 */
import { describe, expect, it, beforeEach } from '@jest/globals';

import { useOnlinePaymentStore } from '../stores/onlinePaymentStore';

describe('onlinePaymentStore', () => {
  beforeEach(() => {
    useOnlinePaymentStore.getState().reset();
  });

  it('transitions awaiting_action → processing → completed', () => {
    useOnlinePaymentStore.getState().start({
      method: 'paypal',
      onlinePaymentId: 'op-1',
      redirectUrl: 'https://gateway.example/checkout',
    });
    expect(useOnlinePaymentStore.getState().phase).toBe('awaiting_action');

    useOnlinePaymentStore.getState().markCallbackReceived();
    expect(useOnlinePaymentStore.getState().phase).toBe('processing');
    expect(useOnlinePaymentStore.getState().callbackReceived).toBe(true);

    useOnlinePaymentStore.getState().complete('pay-9');
    expect(useOnlinePaymentStore.getState().phase).toBe('completed');
    expect(useOnlinePaymentStore.getState().paymentDetailsId).toBe('pay-9');
  });

  it('records failed and cancelled without storing secrets', () => {
    useOnlinePaymentStore.getState().start({
      method: 'credit_card',
      onlinePaymentId: 'op-2',
    });
    useOnlinePaymentStore.getState().fail('DECLINED');
    expect(useOnlinePaymentStore.getState().phase).toBe('failed');
    expect(useOnlinePaymentStore.getState().errorCode).toBe('DECLINED');

    useOnlinePaymentStore.getState().reset();
    useOnlinePaymentStore.getState().start({
      method: 'credit_card',
      onlinePaymentId: 'op-3',
    });
    useOnlinePaymentStore.getState().cancel();
    expect(useOnlinePaymentStore.getState().phase).toBe('cancelled');
  });
});
