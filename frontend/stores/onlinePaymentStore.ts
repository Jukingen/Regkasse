import { create } from 'zustand';

import { logger } from '../lib/logger';
import type { HostedOnlinePaymentCode } from '../services/payment/onlinePaymentMethods';

/**
 * Ephemeral hosted-gateway payment UI state (not persisted).
 * Do not store tokens, PAN, voucher codes, or PII here.
 */
export type OnlinePaymentPhase =
  | 'idle'
  | 'awaiting_action'
  | 'processing'
  | 'completed'
  | 'failed'
  | 'cancelled';

export type OnlinePaymentState = {
  phase: OnlinePaymentPhase;
  method: HostedOnlinePaymentCode | null;
  onlinePaymentId: string | null;
  redirectUrl: string | null;
  paymentDetailsId: string | null;
  errorCode: string | null;
  callbackReceived: boolean;
};

type OnlinePaymentStore = OnlinePaymentState & {
  start: (input: {
    method: HostedOnlinePaymentCode;
    onlinePaymentId: string;
    redirectUrl?: string | null;
  }) => void;
  setPhase: (phase: OnlinePaymentPhase) => void;
  markCallbackReceived: () => void;
  complete: (paymentDetailsId?: string | null) => void;
  fail: (errorCode?: string | null) => void;
  cancel: () => void;
  reset: () => void;
};

const INITIAL: OnlinePaymentState = {
  phase: 'idle',
  method: null,
  onlinePaymentId: null,
  redirectUrl: null,
  paymentDetailsId: null,
  errorCode: null,
  callbackReceived: false,
};

export const useOnlinePaymentStore = create<OnlinePaymentStore>((set, get) => ({
  ...INITIAL,
  start: (input) => {
    logger.info('online_payment.start', {
      method: input.method,
      onlinePaymentId: input.onlinePaymentId,
      hasRedirectUrl: Boolean(input.redirectUrl),
    });
    set({
      phase: input.redirectUrl ? 'awaiting_action' : 'processing',
      method: input.method,
      onlinePaymentId: input.onlinePaymentId,
      redirectUrl: input.redirectUrl ?? null,
      paymentDetailsId: null,
      errorCode: null,
      callbackReceived: false,
    });
  },
  setPhase: (phase) => {
    const prev = get().phase;
    if (prev === phase) return;
    logger.info('online_payment.phase', {
      from: prev,
      to: phase,
      onlinePaymentId: get().onlinePaymentId,
    });
    set({ phase });
  },
  markCallbackReceived: () => {
    logger.info('online_payment.callback_received', {
      onlinePaymentId: get().onlinePaymentId,
    });
    set({ callbackReceived: true, phase: 'processing' });
  },
  complete: (paymentDetailsId) => {
    logger.info('online_payment.completed', {
      onlinePaymentId: get().onlinePaymentId,
      hasPaymentDetailsId: Boolean(paymentDetailsId),
    });
    set({
      phase: 'completed',
      paymentDetailsId: paymentDetailsId ?? null,
      errorCode: null,
    });
  },
  fail: (errorCode) => {
    logger.warn('online_payment.failed', {
      onlinePaymentId: get().onlinePaymentId,
      errorCode: errorCode ?? 'ONLINE_PAYMENT_FAILED',
    });
    set({
      phase: 'failed',
      errorCode: errorCode ?? 'ONLINE_PAYMENT_FAILED',
    });
  },
  cancel: () => {
    logger.info('online_payment.cancelled', {
      onlinePaymentId: get().onlinePaymentId,
    });
    set({ phase: 'cancelled', errorCode: 'ONLINE_PAYMENT_CANCELLED' });
  },
  reset: () => {
    if (get().phase !== 'idle') {
      logger.info('online_payment.reset', { onlinePaymentId: get().onlinePaymentId });
    }
    set({ ...INITIAL });
  },
}));

export const onlinePaymentStoreActions = {
  start: useOnlinePaymentStore.getState().start,
  setPhase: (phase: OnlinePaymentPhase) => useOnlinePaymentStore.getState().setPhase(phase),
  markCallbackReceived: () => useOnlinePaymentStore.getState().markCallbackReceived(),
  complete: (paymentDetailsId?: string | null) =>
    useOnlinePaymentStore.getState().complete(paymentDetailsId),
  fail: (errorCode?: string | null) => useOnlinePaymentStore.getState().fail(errorCode),
  cancel: () => useOnlinePaymentStore.getState().cancel(),
  reset: () => useOnlinePaymentStore.getState().reset(),
};

export const selectOnlinePaymentPhase = (s: OnlinePaymentStore) => s.phase;
export const selectOnlinePaymentId = (s: OnlinePaymentStore) => s.onlinePaymentId;
