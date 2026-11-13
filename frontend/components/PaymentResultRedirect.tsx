import { Redirect, useLocalSearchParams } from 'expo-router';
import { useEffect } from 'react';

import { logger } from '../lib/logger';
import {
  isRedirectStatusCancelled,
  isRedirectStatusFailed,
  parsePaymentResultParams,
} from '../services/payment/parsePaymentResultParams';
import { onlinePaymentStoreActions } from '../stores/onlinePaymentStore';

/**
 * Shared Stripe / hosted-checkout return handler.
 * Parses payment_intent_client_secret and redirect_status; never stores the secret.
 */
export function PaymentResultRedirect() {
  const rawParams = useLocalSearchParams() as Record<string, string | string[] | undefined>;

  useEffect(() => {
    const parsed = parsePaymentResultParams(rawParams);
    logger.info('online_payment.return_route', {
      hasPaymentIntent: Boolean(parsed.paymentIntentId),
      hasClientSecret: Boolean(parsed.paymentIntentClientSecret),
      redirectStatus: parsed.redirectStatus ?? null,
    });

    if (isRedirectStatusFailed(parsed.redirectStatus)) {
      onlinePaymentStoreActions.fail('ONLINE_PAYMENT_FAILED');
      return;
    }
    if (isRedirectStatusCancelled(parsed.redirectStatus)) {
      onlinePaymentStoreActions.cancel();
      return;
    }
    onlinePaymentStoreActions.markCallbackReceived();
  }, [rawParams]);

  return <Redirect href="/(tabs)/cash-register" />;
}
