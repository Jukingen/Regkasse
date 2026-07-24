export interface TseScalingPolicy {
  tenantId: string;
  enabled: boolean;
  minDevices: number;
  maxDevices: number;
  targetTransactionsPerDevice: number;
  scaleUpThreshold: number;
  scaleDownThreshold: number;
  cooldownMinutes: number;
  autoProvision: boolean;
  updatedAt?: string | null;
}

export interface TseScalingHistoryItem {
  id: string;
  timestamp: string;
  action: string;
  from: number;
  to: number;
  loadPercent: number;
  reason: string;
  applied: boolean;
  simulationOnly: boolean;
  actorUserId?: string | null;
}

export interface TseScalingStatus {
  tenantId: string;
  tenantName?: string | null;
  scalingEnabled: boolean;
  currentDevices: number;
  recommendedDevices: number;
  currentLoadPercent: number;
  policy: TseScalingPolicy;
  lastEvaluation?: TseScalingHistoryItem | null;
}

export interface TseScalingResult {
  tenantId: string;
  tenantName?: string | null;
  evaluatedAt: string;
  action: string;
  currentDevices: number;
  recommendedDevices: number;
  currentLoadPercent: number;
  applied: boolean;
  simulationOnly: boolean;
  reason: string;
  policy: TseScalingPolicy;
}

export interface TseScalingHistory {
  tenantId: string;
  items: TseScalingHistoryItem[];
}
