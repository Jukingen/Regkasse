export interface TseDailyTransactionTrend {
  date: string;
  transactionCount: number;
  signedCount: number;
}

export interface TseCapacityReport {
  tenantId: string;
  tenantName?: string | null;
  tenantSlug?: string | null;
  generatedAt: string;
  periodStart: string;
  periodEnd: string;
  dailyTransactionAverage: number;
  monthlyTransactionTotal: number;
  peakHourlyTransactions: number;
  activeSigningDevices: number;
  maxDailyCapacity: number;
  maxHourlyCapacity: number;
  currentUtilizationPercentage: number;
  estimatedNextMonthTransactions: number;
  estimatedCapacityReachDate?: string | null;
  isNearCapacity: boolean;
  lookbackDays: number;
  dailyGrowthRatePercent: number;
  dailyTrends: TseDailyTransactionTrend[];
  recommendations: string[];
}

export interface TseForecastDayPoint {
  date: string;
  estimatedTransactions: number;
}

export interface TseForecastResult {
  tenantId: string;
  forecastDays: number;
  generatedAt: string;
  baselineDailyAverage: number;
  estimatedTotalTransactions: number;
  estimatedDailyAverage: number;
  estimatedPeakHourly: number;
  dailyGrowthRatePercent: number;
  confidence: string;
  dailyPoints: TseForecastDayPoint[];
}

export interface TseCapacityAlert {
  tenantId: string;
  hasAlert: boolean;
  isNearCapacity: boolean;
  severity: string;
  codes: string[];
  message: string;
  utilizationPercentage: number;
  estimatedCapacityReachDate?: string | null;
  alertPublished: boolean;
  report?: TseCapacityReport | null;
}
