export interface TseHealingRule {
  id: string;
  condition: string;
  action: string;
  priority: number;
  status: string;
  lastTriggeredAt?: string | null;
}

export interface TseHealingConfiguration {
  tenantId: string;
  enabled: boolean;
  maxAutoHealAttempts: number;
  cooldownMinutes: number;
  notifyOnHeal: boolean;
  allowAutoFailover: boolean;
  rules: TseHealingRule[];
  diagnosticOnly: boolean;
}

export interface ConfigureTseHealingRequest {
  enabled: boolean;
  maxAutoHealAttempts: number;
  cooldownMinutes: number;
  notifyOnHeal: boolean;
  allowAutoFailover: boolean;
  rules?: Array<{
    id?: string;
    condition: string;
    action: string;
    priority: number;
    status: string;
  }>;
}

export interface TseHealingResult {
  deviceId: string;
  tenantId?: string | null;
  success: boolean;
  applied: boolean;
  status: string;
  message: string;
  healthScoreBefore: number;
  healthScoreAfter?: number | null;
  matchedCondition?: string | null;
  actionTaken?: string | null;
  historyId?: string | null;
  diagnosticOnly: boolean;
}

export interface TseHealingHistoryItem {
  id: string;
  deviceId: string;
  condition?: string | null;
  action?: string | null;
  status: string;
  applied: boolean;
  healthScoreBefore: number;
  healthScoreAfter?: number | null;
  message?: string | null;
  startedAt: string;
  completedAt?: string | null;
}

export interface TseHealingReport {
  tenantId: string;
  generatedAt: string;
  totalAttempts: number;
  appliedCount: number;
  succeededCount: number;
  items: TseHealingHistoryItem[];
  diagnosticOnly: boolean;
}
