import { AXIOS_INSTANCE } from '@/lib/axios';

import type { CustomerAnalyticsDto } from '@/features/super-admin/api/adminTenants';

export type { CustomerAnalyticsDto, PlanDistributionDto } from '@/features/super-admin/api/adminTenants';

export type TseDailyUsageDto = {
  date: string;
  signatures: number;
};

export type TseAnalyticsDto = {
  totalRegisters: number;
  activeRegisters: number;
  tseEnabled: number;
  tseDisabled: number;
  signaturesToday: number;
  signaturesThisMonth: number;
  failedSignatures: number;
  averageSignaturesPerRegister: number;
  dailyUsage: TseDailyUsageDto[];
  diagnosticOnly: boolean;
};

export type DailyVolumeDto = {
  date: string;
  revenue: number;
  transactionCount: number;
};

export type MonthlyVolumeDto = {
  yearMonth: string;
  revenue: number;
  transactionCount: number;
};

export type PaymentVolumeAnalyticsDto = {
  totalRevenue: number;
  revenueThisMonth: number;
  revenueLastMonth: number;
  monthlyGrowth: number;
  totalTransactions: number;
  transactionsThisMonth: number;
  transactionsLastMonth: number;
  averageTransactionValue: number;
  dailyVolume: DailyVolumeDto[];
  monthlyVolume: MonthlyVolumeDto[];
};

export const platformAnalyticsQueryKeys = {
  customers: ['admin', 'analytics', 'customers'] as const,
  tse: (from?: string, to?: string) => ['admin', 'analytics', 'tse', from ?? '', to ?? ''] as const,
  paymentVolume: (from?: string, to?: string, groupBy?: string) =>
    ['admin', 'analytics', 'payment-volume', from ?? '', to ?? '', groupBy ?? 'month'] as const,
};

export async function getCustomerAnalytics(): Promise<CustomerAnalyticsDto> {
  const { data } = await AXIOS_INSTANCE.get<CustomerAnalyticsDto>('/api/admin/analytics/customers');
  return data;
}

export async function getTseAnalytics(fromDate?: string, toDate?: string): Promise<TseAnalyticsDto> {
  const { data } = await AXIOS_INSTANCE.get<TseAnalyticsDto>('/api/admin/analytics/tse', {
    params: { fromDate, toDate },
  });
  return data;
}

export async function getPaymentVolumeAnalytics(
  fromDate?: string,
  toDate?: string,
  groupBy: 'day' | 'week' | 'month' = 'month'
): Promise<PaymentVolumeAnalyticsDto> {
  const { data } = await AXIOS_INSTANCE.get<PaymentVolumeAnalyticsDto>('/api/admin/analytics/payment-volume', {
    params: { fromDate, toDate, groupBy },
  });
  return data;
}
