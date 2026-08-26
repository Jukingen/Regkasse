import { PaymentResultRedirect } from '../../components/PaymentResultRedirect';

/** Hidden tab: Stripe return URL target for in-app `/payment` (query: payment_intent_client_secret, redirect_status). */
export default function PaymentTabResultScreen() {
  return <PaymentResultRedirect />;
}
