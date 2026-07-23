export type TseHealthStatusLabel =
  | 'Healthy'
  | 'Degraded'
  | 'Unhealthy'
  | 'Offline'
  | 'Expired'
  | 'Revoked'
  | string;

export interface TseFailoverDevice {
  id: string;
  deviceId?: string | null;
  serialNumber: string;
  provider?: string | null;
  deviceType: string;
  tenantId?: string | null;
  tenantName?: string | null;
  tenantSlug?: string | null;
  cashRegisterId?: string | null;
  cashRegisterNumber?: string | null;
  isPrimary: boolean;
  isBackup: boolean;
  isActive: boolean;
  isFailoverActive: boolean;
  primaryDeviceId?: string | null;
  healthStatus: TseHealthStatusLabel;
  healthScore: number;
  healthMessage?: string | null;
  lastHealthCheck?: string | null;
  failoverCount: number;
  lastFailoverAt?: string | null;
  lastFailoverReason?: string | null;
}

export interface TseActiveFailover {
  id: string;
  primaryDeviceId: string;
  primarySerialNumber?: string | null;
  backupDeviceId: string;
  backupSerialNumber?: string | null;
  tenantId?: string | null;
  tenantName?: string | null;
  lastFailoverAt?: string | null;
  lastFailoverReason?: string | null;
}

export interface TseFailoverStatus {
  activeFailoverCount: number;
  healthyDeviceCount: number;
  activeDeviceCount: number;
  backupAvailableCount: number;
  autoFailoverEnabled: boolean;
  activeFailovers: TseActiveFailover[];
}

export interface TseFailoverHistoryItem {
  id: string;
  tenantId: string;
  primaryDeviceId: string;
  backupDeviceId?: string | null;
  failoverType: string;
  triggerReason: string;
  previousStatus?: string | null;
  newStatus?: string | null;
  isSuccessful: boolean;
  errorMessage?: string | null;
  startedAt: string;
  completedAt?: string | null;
  performedBy?: string | null;
  notes?: string | null;
}

export interface ManualTseFailoverRequest {
  primaryDeviceId: string;
  backupDeviceId: string;
}

export interface RevertTseFailoverRequest {
  primaryDeviceId: string;
}

export interface TseFailoverActionResponse {
  success: boolean;
  message: string;
  failoverType?: string | null;
  primaryDeviceId?: string | null;
  backupDeviceId?: string | null;
  logId?: string | null;
  needsAttention: boolean;
}

export interface TseDeviceHealthSummary {
  deviceId: string;
  vendorDeviceId?: string | null;
  serialNumber: string;
  isPrimary: boolean;
  isBackup: boolean;
  isFailoverActive: boolean;
  healthStatus: TseHealthStatusLabel;
  healthScore: number;
  healthMessage?: string | null;
  lastHealthCheck?: string | null;
}

export interface TseHealthAlert {
  id: string;
  source: string;
  type: string;
  severity: string;
  title: string;
  description?: string | null;
  atUtc: string;
}

export interface TseHealthRecommendation {
  code: string;
  severity: string;
  message: string;
  deviceId?: string | null;
}

export interface TseHealthReport {
  tenantId: string;
  tenantName?: string | null;
  tenantSlug?: string | null;
  generatedAt: string;
  totalDevices: number;
  healthyDevices: number;
  degradedDevices: number;
  unhealthyDevices: number;
  averageHealthScore: number;
  minHealthScore: number;
  maxHealthScore: number;
  healthyMinScore: number;
  degradedMinScore: number;
  deviceSummaries: TseDeviceHealthSummary[];
  recentAlerts: TseHealthAlert[];
  recommendations: TseHealthRecommendation[];
}

export interface TseHealthTrendPoint {
  date: string;
  deviceId: string;
  deviceLabel?: string | null;
  score: number;
  healthStatus: TseHealthStatusLabel;
}

export interface TsePerformancePoint {
  timestamp: string;
  responseTimeMs?: number | null;
  success: boolean;
  healthScore: number;
  healthStatus: TseHealthStatusLabel;
}

export interface TsePerformanceMetrics {
  deviceId: string;
  deviceLabel?: string | null;
  tenantId?: string | null;
  startDate: string;
  endDate: string;
  averageResponseTime: number;
  minResponseTime: number;
  maxResponseTime: number;
  timedSamples: number;
  totalRequests: number;
  successfulRequests: number;
  failedRequests: number;
  successRate: number;
  errorRate: number;
  slowThresholdMs: number;
  criticalThresholdMs: number;
  performanceHistory: TsePerformancePoint[];
}

export interface TsePerformanceAlert {
  deviceId: string;
  tenantId?: string | null;
  hasAnomaly: boolean;
  severity: string;
  codes: string[];
  message: string;
  alertPublished: boolean;
  metrics?: TsePerformanceMetrics | null;
}

export interface TseComplianceIssue {
  code: string;
  severity: string;
  message: string;
  cashRegisterId?: string | null;
  deviceId?: string | null;
  count?: number | null;
}

export interface TseComplianceRecommendation {
  code: string;
  severity: string;
  message: string;
  deviceId?: string | null;
}

export interface TseComplianceHealthSummary {
  totalDevices: number;
  healthyDevices: number;
  degradedDevices: number;
  unhealthyDevices: number;
  averageHealthScore: number;
  healthyMinScore: number;
  degradedMinScore: number;
}

export interface TseComplianceSignatureChainSummary {
  registersChecked: number;
  receiptsChecked: number;
  signatureCount: number;
  chainBreakCount: number;
  sequenceGapCount: number;
  duplicateCount: number;
  missingSignatureCount: number;
  chainHealthy: boolean;
}

export interface TseComplianceReport {
  tenantId: string;
  tenantName?: string | null;
  tenantSlug?: string | null;
  reportPeriodStart: string;
  reportPeriodEnd: string;
  generatedAt: string;
  totalTransactions: number;
  signedTransactions: number;
  unsignedTransactions: number;
  isFullyCompliant: boolean;
  issues: TseComplianceIssue[];
  recommendations: TseComplianceRecommendation[];
  healthSummary: TseComplianceHealthSummary;
  signatureChainSummary: TseComplianceSignatureChainSummary;
  legalNoticeDe: string;
}

export interface TseComplianceStatus {
  tenantId: string;
  tenantName?: string | null;
  status: 'Compliant' | 'AtRisk' | 'NonCompliant' | string;
  isFullyCompliant: boolean;
  totalTransactions: number;
  unsignedTransactions: number;
  chainBreakCount: number;
  unhealthyDevices: number;
  checkedAt: string;
  lookbackStart: string;
  lookbackEnd: string;
  topIssueCodes: string[];
}
