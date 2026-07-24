export type TseLogLevel = 'Error' | 'Warning' | 'Info' | string;

export interface TseLogEntry {
  id: string;
  tenantId: string;
  timestamp: string;
  level: TseLogLevel;
  source: string;
  message: string;
  deviceId?: string | null;
  deviceLabel?: string | null;
  provider?: string | null;
  category?: string | null;
  metadata?: Record<string, string> | null;
}

export interface TseLogPattern {
  pattern: string;
  count: number;
  level: string;
  sampleMessage?: string | null;
}

export interface TseLogAnomaly {
  code: string;
  severity: string;
  message: string;
  detectedAt: string;
  score: number;
}

export interface TseLogAggregationResult {
  tenantId: string;
  tenantName?: string | null;
  periodStart: string;
  periodEnd: string;
  totalLogs: number;
  errorLogs: number;
  warningLogs: number;
  infoLogs: number;
  logsByProvider: Record<string, number>;
  logsByDevice: Record<string, number>;
  logsBySource: Record<string, number>;
  patterns: TseLogPattern[];
  anomalies: TseLogAnomaly[];
  recentLogs: TseLogEntry[];
}

export interface TseLogSearchResult {
  tenantId: string;
  totalMatched: number;
  skip: number;
  take: number;
  logs: TseLogEntry[];
}

export interface TseLogAnalysisReport {
  tenantId: string;
  periodStart: string;
  periodEnd: string;
  generatedAt: string;
  summary: string;
  errorRatePercent: number;
  warningRatePercent: number;
  topPatterns: TseLogPattern[];
  anomalies: TseLogAnomaly[];
  recommendations: string[];
  aggregation: TseLogAggregationResult;
}
