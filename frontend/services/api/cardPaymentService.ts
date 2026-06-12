import { apiClient } from './config';

export type CardPaymentIntentResponse = {
  id: string;
  amount: number;
  currency: string;
  status: string;
  gatewayProvider: string;
  clientSecret?: string | null;
  transactionId?: string | null;
  cardBrand?: string | null;
  lastFourDigits?: string | null;
  errorMessage?: string | null;
  cashRegisterId: string;
  paymentDetailsId?: string | null;
  createdAtUtc: string;
  confirmedAtUtc?: string | null;
};

export type CardPaymentConfirmResponse = {
  success: boolean;
  transactionId?: string | null;
  errorMessage?: string | null;
};

export async function createCardPaymentIntent(params: {
  amount: number;
  cashRegisterId: string;
  receiptNumber?: string;
}): Promise<CardPaymentIntentResponse> {
  const res = await apiClient.post<CardPaymentIntentResponse>('/api/pos/card-payment/intent', params);
  return res.data;
}

export async function confirmCardPayment(params: {
  paymentIntentId: string;
  paymentMethodId?: string;
}): Promise<CardPaymentConfirmResponse> {
  const res = await apiClient.post<CardPaymentConfirmResponse>('/api/pos/card-payment/confirm', params);
  return res.data;
}

export async function confirmCardPaymentIntent(
  intentId: string,
  paymentMethodId: string,
): Promise<CardPaymentIntentResponse> {
  const res = await apiClient.post<CardPaymentIntentResponse>(
    `/api/pos/payment/card/${intentId}/confirm`,
    { paymentMethodId },
  );
  return res.data;
}

export async function cancelCardPaymentIntent(intentId: string): Promise<CardPaymentIntentResponse> {
  const res = await apiClient.post<CardPaymentIntentResponse>(`/api/pos/payment/card/${intentId}/cancel`);
  return res.data;
}

/** Stripe-style test card simulation: create + confirm in one step. */
export async function simulateCardCharge(params: {
  amount: number;
  cashRegisterId: string;
  paymentMethodId: string;
  receiptNumber?: string;
}): Promise<CardPaymentIntentResponse & { confirmSuccess: boolean }> {
  const intent = await createCardPaymentIntent({
    amount: params.amount,
    cashRegisterId: params.cashRegisterId,
    receiptNumber: params.receiptNumber,
  });
  const confirm = await confirmCardPayment({
    paymentIntentId: intent.id,
    paymentMethodId: params.paymentMethodId,
  });
  return {
    ...intent,
    status: confirm.success ? 'Succeeded' : 'Failed',
    errorMessage: confirm.errorMessage,
    confirmSuccess: confirm.success,
  };
}
