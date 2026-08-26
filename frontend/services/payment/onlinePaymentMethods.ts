import type { PaymentMethod } from '../api/paymentService';

/** Hosted-gateway method codes for POST /api/pos/payment/initiate. */
export const HOSTED_ONLINE_PAYMENT_CODES = ['credit_card', 'paypal'] as const;

export type HostedOnlinePaymentCode = (typeof HOSTED_ONLINE_PAYMENT_CODES)[number];

const HOSTED_SET = new Set<string>(HOSTED_ONLINE_PAYMENT_CODES);

export function isHostedOnlinePaymentMethod(type: string | null | undefined): boolean {
  if (!type) return false;
  return HOSTED_SET.has(type.trim().toLowerCase());
}

export type HostedOnlineMethodSeed = {
  id: HostedOnlinePaymentCode;
  name: string;
  type: HostedOnlinePaymentCode;
  icon: string;
};

export const HOSTED_ONLINE_METHOD_SEEDS: readonly HostedOnlineMethodSeed[] = [
  {
    id: 'credit_card',
    name: 'Kreditkarte',
    type: 'credit_card',
    icon: 'card-outline',
  },
  {
    id: 'paypal',
    name: 'PayPal',
    type: 'paypal',
    icon: 'logo-paypal',
  },
] as const;

/**
 * Ensures Kreditkarte and PayPal chips are visible even when the catalog
 * has not yet returned those codes. Catalog names win when the code exists.
 */
export function mergeHostedOnlinePaymentMethods(methods: PaymentMethod[]): PaymentMethod[] {
  const list = [...methods];
  for (const seed of HOSTED_ONLINE_METHOD_SEEDS) {
    if (list.some((m) => m.type === seed.type)) continue;
    list.push({
      id: seed.id,
      name: seed.name,
      type: seed.type,
      icon: seed.icon,
      requiresReceivedAmount: false,
    });
  }
  return list;
}

export function isOnlinePaymentDisabledOffline(
  methodType: string | null | undefined,
  isOnline: boolean
): boolean {
  return isHostedOnlinePaymentMethod(methodType) && !isOnline;
}
