export type TseAnomalySeverity = 'Critical' | 'High' | 'Medium' | 'Low' | 'Info' | string;

export interface TseAnomaly {
  id: string;
  tenantId: string;
  deviceId?: string | null;
  deviceLabel?: string | null;
  metricName: string;
  currentValue: number;
  expectedValue: number;
  deviation: number;
  severity: TseAnomalySeverity;
  description: string;
  suggestedAction?: string | null;
  detectedAt: string;
  isResolved: boolean;
  resolvedAt?: string | null;
}

export interface TseAnomalyResult {
  tenantId: string;
  deviceId?: string | null;
  detectedAt: string;
  anomalies: TseAnomaly[];
  overallSeverity: TseAnomalySeverity;
  requiresAction: boolean;
  summary: string;
  diagnosticOnly: boolean;
}

export interface TseAnomalyDashboard {
  tenantId: string;
  tenantName?: string | null;
  criticalCount: number;
  highCount: number;
  mediumCount: number;
  lowCount: number;
  infoCount: number;
  openCount: number;
  lastDetection?: TseAnomalyResult | null;
  anomalies: TseAnomaly[];
}
