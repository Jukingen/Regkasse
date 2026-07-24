export type TseDrScenario = 'TSEFailure' | 'NetworkIsolation' | 'DataCorruption' | string;
export type TseDrStatusValue = 'Ready' | 'InProgress' | 'Completed' | 'Failed' | string;

export interface TseDrStep {
  id: string;
  order: number;
  action: string;
  description: string;
  isAutomated: boolean;
  isCompleted: boolean;
  startedAt?: string | null;
  completedAt?: string | null;
  result?: string | null;
  error?: string | null;
}

export interface TseDrRunbook {
  id: string;
  tenantId: string;
  tenantName?: string | null;
  name: string;
  scenario: TseDrScenario;
  status: TseDrStatusValue;
  createdAt: string;
  lastTestedAt?: string | null;
  estimatedRtoMinutes: number;
  actualRtoMinutes: number;
  isDrill: boolean;
  summary?: string | null;
  steps: TseDrStep[];
}

export interface TseDrStatus {
  tenantId: string;
  tenantName?: string | null;
  isReady: boolean;
  lastDrillAt?: string | null;
  rtoTargetMinutes: number;
  rtoActualMinutes: number;
  primaryDeviceCount: number;
  healthyBackupCount: number;
  readinessMessage: string;
  latestRunbookId?: string | null;
  latestRunbook?: TseDrRunbook | null;
}

export interface TseDrExecutionResult {
  runbookId: string;
  tenantId: string;
  status: string;
  success: boolean;
  actualRtoMinutes: number;
  completedSteps: number;
  failedSteps: number;
  skippedManualSteps: number;
  message: string;
  simulationOnly: boolean;
  runbook: TseDrRunbook;
}

export interface TseDrReport {
  tenantId: string;
  runbookId: string;
  scenario: string;
  success: boolean;
  startedAt: string;
  completedAt: string;
  actualRtoMinutes: number;
  rtoTargetMinutes: number;
  metRtoTarget: boolean;
  summary: string;
  findings: string[];
  execution: TseDrExecutionResult;
  statusAfter: TseDrStatus;
}
