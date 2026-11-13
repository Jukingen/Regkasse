import { customInstance } from '@/lib/axios';

export type TaxGroupSummary = {
  taxGroupName: string;
  rate: number;
  netRevenue: number;
  taxAmount: number;
  grossRevenue: number;
  transactionCount: number;
};

export type TaxReport = {
  periodStart: string;
  periodEnd: string;
  taxGroups: TaxGroupSummary[];
  totalNetRevenue: number;
  totalTaxAmount: number;
  totalGrossRevenue: number;
};

export type TaxTrendPoint = {
  date: string;
  rate: number;
  taxRateLabel: string;
  amount: number;
};

export const taxReportQueryKey = ['tax-report'] as const;
export const taxTrendQueryKey = ['tax-report', 'trend'] as const;

export async function getTaxReport(params: {
  fromUtc: string;
  toUtc: string;
}): Promise<TaxReport> {
  return customInstance<TaxReport>({
    url: '/api/admin/reports/tax',
    method: 'GET',
    params,
  });
}

export async function getTaxTrend(params: {
  fromUtc: string;
  toUtc: string;
  granularity?: 'day' | 'month';
}): Promise<TaxTrendPoint[]> {
  return customInstance<TaxTrendPoint[]>({
    url: '/api/admin/reports/tax/trend',
    method: 'GET',
    params,
  });
}

export async function downloadTaxReportCsv(params: {
  period: 'year' | 'month' | 'custom';
  fromUtc?: string;
  toUtc?: string;
}): Promise<Blob> {
  return customInstance<Blob>({
    url: '/api/admin/reports/tax/export',
    method: 'GET',
    params,
    responseType: 'blob',
  });
}
