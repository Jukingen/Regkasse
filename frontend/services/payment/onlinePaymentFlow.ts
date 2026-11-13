import { logger } from '../../lib/logger';
import {
  paymentService,
  type InitiateOnlinePaymentRequest,
  type OnlinePaymentStatusResponse,
} from '../api/paymentService';
import { onlinePaymentStoreActions } from '../../stores/onlinePaymentStore';
import type { HostedOnlinePaymentCode } from './onlinePaymentMethods';
import {
  createOnlinePaymentReturnUrl,
  openOnlinePaymentHostedPage,
} from './openOnlinePaymentHostedPage';

export class OnlinePaymentFlowError extends Error {
  readonly code: string;

  constructor(code: string, message: string) {
    super(message);
    this.name = 'OnlinePaymentFlowError';
    this.code = code;
  }
}

function mapTerminalError(status: OnlinePaymentStatusResponse): OnlinePaymentFlowError | null {
  if (status.status === 'completed') return null;
  if (status.status === 'cancelled') {
    return new OnlinePaymentFlowError(
      status.errorCode ?? 'ONLINE_PAYMENT_CANCELLED',
      'Online-Zahlung abgebrochen.'
    );
  }
  if (status.status === 'failed') {
    return new OnlinePaymentFlowError(
      status.errorCode ?? 'ONLINE_PAYMENT_FAILED',
      'Online-Zahlung fehlgeschlagen.'
    );
  }
  return new OnlinePaymentFlowError(
    status.errorCode ?? 'ONLINE_PAYMENT_TIMEOUT',
    'Zeitüberschreitung bei der Online-Zahlung.'
  );
}

export type RunHostedOnlinePaymentInput = {
  method: HostedOnlinePaymentCode;
  cashRegisterId: string;
  amount: number;
  customerId?: string;
  tableNumber?: number;
  signal?: AbortSignal;
};

/**
 * Initiate → hosted page / redirect → poll status until terminal.
 * Does not create the fiscal POS payment; caller continues with processPayment.
 */
export async function runHostedOnlinePayment(
  input: RunHostedOnlinePaymentInput
): Promise<OnlinePaymentStatusResponse> {
  const returnUrl = createOnlinePaymentReturnUrl();
  const idempotencyKey =
    typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
      ? crypto.randomUUID()
      : `${Date.now()}-${Math.random().toString(36).slice(2, 15)}`;

  const request: InitiateOnlinePaymentRequest = {
    cashRegisterId: input.cashRegisterId,
    amount: input.amount,
    method: input.method,
    idempotencyKey,
    currency: 'EUR',
    customerId: input.customerId,
    tableNumber: input.tableNumber,
    returnUrl,
    cancelUrl: returnUrl,
  };

  logger.info('online_payment.flow_start', { method: input.method });
  onlinePaymentStoreActions.setPhase('processing');

  const initiated = await paymentService.initiateOnlinePayment(request);
  const hostedUrl = initiated.redirectUrl ?? initiated.hostedUrl ?? null;
  onlinePaymentStoreActions.start({
    method: input.method,
    onlinePaymentId: initiated.onlinePaymentId,
    redirectUrl: hostedUrl,
  });

  if (initiated.status === 'completed') {
    onlinePaymentStoreActions.complete(initiated.paymentDetailsId ?? null);
    return {
      onlinePaymentId: initiated.onlinePaymentId,
      status: 'completed',
      paymentDetailsId: initiated.paymentDetailsId,
      redirectUrl: hostedUrl,
    };
  }

  if (hostedUrl) {
    const pageResult = await openOnlinePaymentHostedPage(hostedUrl, returnUrl);
    if (pageResult.kind === 'callback') {
      onlinePaymentStoreActions.markCallbackReceived();
    } else if (pageResult.kind === 'failed') {
      onlinePaymentStoreActions.fail('HOSTED_PAGE_OPEN_FAILED');
      throw new OnlinePaymentFlowError(
        'HOSTED_PAGE_OPEN_FAILED',
        'Zahlungsseite konnte nicht geöffnet werden.'
      );
    } else {
      onlinePaymentStoreActions.setPhase('processing');
    }
  }

  const status = await paymentService.pollOnlinePaymentStatus(initiated.onlinePaymentId, {
    signal: input.signal,
    onStatus: (s) => {
      if (s.status === 'awaiting_action' || s.status === 'pending') {
        onlinePaymentStoreActions.setPhase('awaiting_action');
      } else if (s.status === 'processing') {
        onlinePaymentStoreActions.setPhase('processing');
      }
    },
  });

  const err = mapTerminalError(status);
  if (err) {
    if (status.status === 'cancelled') onlinePaymentStoreActions.cancel();
    else onlinePaymentStoreActions.fail(err.code);
    throw err;
  }

  onlinePaymentStoreActions.complete(status.paymentDetailsId ?? status.paymentId ?? null);
  logger.info('online_payment.flow_completed', {
    onlinePaymentId: status.onlinePaymentId,
  });
  return status;
}
