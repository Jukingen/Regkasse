export type TseSlaGrade = 'A' | 'B' | 'C' | 'D' | 'F' | 'N' | string;

export interface TseSlaViolation {
  code: string;
  metric: string;
  severity: string;
  message: string;
  actualValue: number;
  targetValue: number;
  detectedAt: string;
}

export interface TseSlaReport {
  tenantId: string;
  tenantName?: string | null;
  tenantSlug?: string | null;
  periodStart: string;
  periodEnd: string;
  uptimePercentage: number;
  targetUptimePercentage: number;
  isUptimeTargetMet: boolean;
  averageResponseTime: number;
  targetResponseTime: number;
  isResponseTimeTargetMet: boolean;
  totalTransactions: number;
  successfulTransactions: number;
  successRate: number;
  targetSuccessRate: number;
  isSuccessRateTargetMet: boolean;
  healthSampleCount: number;
  timedSampleCount: number;
  violations: TseSlaViolation[];
  grade: TseSlaGrade;
}

export interface TseSlaStatus {
  tenantId: string;
  tenantName?: string | null;
  asOfUtc: string;
  lookbackStartUtc: string;
  grade: TseSlaGrade;
  isCompliant: boolean;
  uptimePercentage: number;
  averageResponseTime: number;
  successRate: number;
  openViolationCount: number;
  report: TseSlaReport;
}

export interface TseSlaAlert {
  tenantId: string;
  hasViolations: boolean;
  severity: string;
  message: string;
  alertPublished: boolean;
  violations: TseSlaViolation[];
  report?: TseSlaReport | null;
}
