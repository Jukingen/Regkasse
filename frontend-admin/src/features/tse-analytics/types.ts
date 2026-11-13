export interface TseBiDailyTrend {
  date: string;
  value: number;
  label: string;
}

export interface TseBiNamedCount {
  name: string;
  count: number;
}

export interface TseBiDashboard {
  tenantId: string;
  tenantName?: string | null;
  generatedAt: string;
  lookbackDays: number;
  totalTransactions: number;
  activeDevices: number;
  totalDevices: number;
  overallHealthScore: number;
  transactionTrends: TseBiDailyTrend[];
  healthTrends: TseBiDailyTrend[];
  statusBreakdown: TseBiNamedCount[];
  providerBreakdown: TseBiNamedCount[];
  criticalAlerts: number;
  warningAlerts: number;
  infoAlerts: number;
  diagnosticOnly: boolean;
}

export interface TseBiExportResult {
  fileName: string;
  contentType: string;
  contentBase64: string;
  byteLength: number;
  diagnosticOnly: boolean;
}
