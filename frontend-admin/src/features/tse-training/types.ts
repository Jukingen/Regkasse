export interface TseTrainingModule {
  id: string;
  title: string;
  description: string;
  estimatedMinutes: number;
  category: string;
  isStarted: boolean;
  isCompleted: boolean;
  completedAt?: string | null;
}

export interface TseTrainingEnvironment {
  modules: TseTrainingModule[];
  completedCount: number;
  totalCount: number;
  simulationEnabled: boolean;
  diagnosticOnly: boolean;
}

export interface TseTrainingConsoleEntry {
  id: string;
  timestampUtc: string;
  level: string;
  scenario: string;
  message: string;
  deviceId?: string | null;
  success: boolean;
}

export interface TseTrainingSimulateResult {
  success: boolean;
  error?: string | null;
  message: string;
  scenario: string;
  consoleEntry?: TseTrainingConsoleEntry | null;
  diagnosticOnly: boolean;
}

export type TseTrainingFailureType =
  | 'NetworkTimeout'
  | 'CertificateExpiry'
  | 'SignatureError';
