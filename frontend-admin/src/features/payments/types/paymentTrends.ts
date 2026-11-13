export type TrendPeriod = 'Daily' | 'Weekly' | 'Monthly';

export type TrendDataPoint = {
  date: string;
  totalAmount: number;
  transactionCount: number;
  averageAmount: number;
  weekNumber?: number | null;
  label?: string | null;
};

export type PaymentMethodComparison = {
  method: string;
  currentAmount: number;
  previousAmount: number;
  changePercentage: number;
};

export type ComparisonData = {
  previousPeriodTotal: number;
  currentPeriodTotal: number;
  growthPercentage: number;
  trend: 'up' | 'down' | 'stable';
  paymentMethodComparison: PaymentMethodComparison[];
};

export type TrendSummary = {
  totalRevenue: number;
  totalTransactions: number;
  averageTransactionValue: number;
  bestDay?: string | null;
  bestDayRevenue: number;
  mostUsedPaymentMethod?: string | null;
  peakHourRevenue: number;
  peakHour: number;
};

export type TrendAnalysisResponse = {
  period: TrendPeriod;
  startDate: string;
  endDate: string;
  trendData: TrendDataPoint[];
  comparison: ComparisonData;
  summary: TrendSummary;
};
