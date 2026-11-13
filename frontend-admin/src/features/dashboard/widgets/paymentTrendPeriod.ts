import type { TrendPeriod } from '@/features/payments/types/paymentTrends';

export function parsePaymentTrendPeriod(value: unknown): TrendPeriod {
  if (value === 'Weekly' || value === 'Monthly') return value;
  return 'Daily';
}
