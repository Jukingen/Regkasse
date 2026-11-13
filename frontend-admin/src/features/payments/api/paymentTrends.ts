import type { TrendAnalysisResponse, TrendPeriod } from '@/features/payments/types/paymentTrends';
import { customInstance } from '@/lib/axios';

export async function fetchPaymentTrends(
  params: { period?: TrendPeriod; startDate?: string; endDate?: string },
  signal?: AbortSignal
): Promise<TrendAnalysisResponse> {
  return customInstance<TrendAnalysisResponse>({
    url: '/api/admin/payments/trends',
    method: 'GET',
    params: {
      period: params.period ?? 'Daily',
      startDate: params.startDate,
      endDate: params.endDate,
    },
    signal,
  });
}
