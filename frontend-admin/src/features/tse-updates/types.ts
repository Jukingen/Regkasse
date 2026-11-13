export type TseUpdateRisk = 'Low' | 'Medium' | 'High' | string;

export interface TseAvailableUpdate {
  updateType: string;
  name: string;
  description: string;
  currentVersion: string;
  targetVersion: string;
  risk: TseUpdateRisk;
  requiresHealthyBackup: boolean;
  zeroDowntime: boolean;
}

export interface TseUpdateStatus {
  tenantId: string;
  hasUpdates: boolean;
  availableUpdates: TseAvailableUpdate[];
  lastChecked: string;
  riskLevel: TseUpdateRisk;
  activeDeviceCount: number;
  zeroDowntimeCapable: boolean;
  diagnosticOnly: boolean;
}

export interface TseUpdateResult {
  tenantId: string;
  updateType: string;
  success: boolean;
  status: string;
  message: string;
  fromVersion?: string | null;
  toVersion?: string | null;
  zeroDowntime: boolean;
  devicesTouched: number;
  historyId?: string | null;
  diagnosticOnly: boolean;
}

export interface TseUpdateHistoryItem {
  id: string;
  updateType: string;
  name: string;
  description: string;
  riskLevel: string;
  fromVersion: string;
  toVersion: string;
  status: string;
  zeroDowntime: boolean;
  devicesTouched: number;
  startedAt: string;
  completedAt?: string | null;
  message?: string | null;
}

export interface TseUpdateHistory {
  tenantId: string;
  generatedAt: string;
  items: TseUpdateHistoryItem[];
  diagnosticOnly: boolean;
}
