import { PaymentResultRedirect } from '../../components/PaymentResultRedirect';

/** Legacy gateway return URL; canonical routes are `/payment/result` and `regkasse://payment-result`. */
export default function OnlinePaymentCallbackScreen() {
  return <PaymentResultRedirect />;
}
