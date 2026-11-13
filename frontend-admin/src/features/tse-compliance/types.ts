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

export interface TseComplianceCertificateRow {
  deviceId: string;
  serialNumber: string;
  provider?: string | null;
  certificateStatus: string;
  lifecycleStatus: string;
  isValid: boolean;
  expiresAt?: string | null;
  daysUntilExpiry?: number | null;
  healthScore: number;
  healthStatus: string;
  scheduledRenewalAt?: string | null;
}

export interface TseComplianceTransactionSummary {
  totalTransactions: number;
  signedTransactions: number;
  unsignedTransactions: number;
  signedPercent: number;
  signatureChainHealthy: boolean;
  chainBreakCount: number;
  sequenceGapCount: number;
  duplicateCount: number;
  missingSignatureCount: number;
  issues: TseComplianceIssue[];
}

export interface TseComplianceAuditTrailItem {
  id: string;
  timestampUtc: string;
  action: string;
  entityType: string;
  entityId?: string | null;
  userId: string;
  userRole: string;
  description?: string | null;
  status: string;
}

export interface TseComplianceDashboard {
  tenantId: string;
  tenantName?: string | null;
  tenantSlug?: string | null;
  periodStart: string;
  periodEnd: string;
  generatedAt: string;
  complianceScore: number;
  status: string;
  signatureChainStatus: string;
  validCertificates: number;
  totalCertificates: number;
  auditLogCount: number;
  report: TseComplianceReport;
  certificates: TseComplianceCertificateRow[];
  transactions: TseComplianceTransactionSummary;
  auditTrail: TseComplianceAuditTrailItem[];
  legalNoticeDe: string;
}
