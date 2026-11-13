import { customInstance } from '@/lib/axios';

export type FinanzOnlineOutboxWorkerNumericDto = {
  effective: number;
  config: number;
  overlay?: number | null;
};

export type FinanzOnlineOutboxWorkerRangeDto = {
  min: number;
  max: number;
  values: number[];
};

export type FinanzOnlineOutboxWorkerAllowedDto = {
  pollIntervalSeconds: FinanzOnlineOutboxWorkerRangeDto;
  maxAttempts: FinanzOnlineOutboxWorkerRangeDto;
  baseDelaySeconds: FinanzOnlineOutboxWorkerRangeDto;
  backoffCapSeconds: FinanzOnlineOutboxWorkerRangeDto;
  jitterMaxSeconds: FinanzOnlineOutboxWorkerRangeDto;
  processingTimeoutSeconds: FinanzOnlineOutboxWorkerRangeDto;
};

export type FinanzOnlineOutboxWorkerSettingsDto = {
  enabled: boolean;
  configEnabled: boolean;
  overrideEnabled?: boolean | null;
  pollIntervalSeconds: FinanzOnlineOutboxWorkerNumericDto;
  maxAttempts: FinanzOnlineOutboxWorkerNumericDto;
  baseDelaySeconds: FinanzOnlineOutboxWorkerNumericDto;
  backoffCapSeconds: FinanzOnlineOutboxWorkerNumericDto;
  jitterMaxSeconds: FinanzOnlineOutboxWorkerNumericDto;
  processingTimeoutSeconds: FinanzOnlineOutboxWorkerNumericDto;
  allowed: FinanzOnlineOutboxWorkerAllowedDto;
  source: string;
  canManage: boolean;
  isProduction: boolean;
};

export type UpdateFinanzOnlineOutboxWorkerRequest = {
  enabled?: boolean;
  pollIntervalSeconds?: number;
  maxAttempts?: number;
  baseDelaySeconds?: number;
  backoffCapSeconds?: number;
  jitterMaxSeconds?: number;
  processingTimeoutSeconds?: number;
  clearOverride?: boolean;
  confirmProductionDisable?: boolean;
};

export async function getFinanzOnlineOutboxWorkerSettings(
  signal?: AbortSignal
): Promise<FinanzOnlineOutboxWorkerSettingsDto> {
  return customInstance<FinanzOnlineOutboxWorkerSettingsDto>({
    url: '/api/admin/finanzonline-outbox/worker-settings',
    method: 'GET',
    signal,
  });
}

export async function updateFinanzOnlineOutboxWorkerSettings(
  body: UpdateFinanzOnlineOutboxWorkerRequest,
  signal?: AbortSignal
): Promise<FinanzOnlineOutboxWorkerSettingsDto> {
  return customInstance<FinanzOnlineOutboxWorkerSettingsDto>({
    url: '/api/admin/finanzonline-outbox/worker-settings',
    method: 'PUT',
    data: body,
    signal,
  });
}

export type FinanzOnlineRuntimeSettingsDto = {
  useSimulation: boolean;
  configUseSimulation: boolean;
  enableRealTestSubmission: boolean;
  configEnableRealTestSubmission: boolean;
  enableRealTestQuery: boolean;
  configEnableRealTestQuery: boolean;
  retryJobEnabled: boolean;
  configRetryJobEnabled: boolean;
  retryIntervalSeconds: FinanzOnlineOutboxWorkerNumericDto;
  retryMaxRetryCount: FinanzOnlineOutboxWorkerNumericDto;
  retryBaseDelaySeconds: FinanzOnlineOutboxWorkerNumericDto;
  retryBackoffCapSeconds: FinanzOnlineOutboxWorkerNumericDto;
  retryBatchSize: FinanzOnlineOutboxWorkerNumericDto;
  allowed: {
    retryIntervalSeconds: FinanzOnlineOutboxWorkerRangeDto;
    retryMaxRetryCount: FinanzOnlineOutboxWorkerRangeDto;
    retryBaseDelaySeconds: FinanzOnlineOutboxWorkerRangeDto;
    retryBackoffCapSeconds: FinanzOnlineOutboxWorkerRangeDto;
    retryBatchSize: FinanzOnlineOutboxWorkerRangeDto;
  };
  source: string;
  canManage: boolean;
  isProduction: boolean;
};

export type UpdateFinanzOnlineRuntimeRequest = {
  useSimulation?: boolean;
  enableRealTestSubmission?: boolean;
  enableRealTestQuery?: boolean;
  retryJobEnabled?: boolean;
  retryIntervalSeconds?: number;
  retryMaxRetryCount?: number;
  retryBaseDelaySeconds?: number;
  retryBackoffCapSeconds?: number;
  retryBatchSize?: number;
  clearOverride?: boolean;
};

export async function getFinanzOnlineRuntimeSettings(
  signal?: AbortSignal
): Promise<FinanzOnlineRuntimeSettingsDto> {
  return customInstance<FinanzOnlineRuntimeSettingsDto>({
    url: '/api/admin/finanzonline-outbox/runtime-settings',
    method: 'GET',
    signal,
  });
}

export async function updateFinanzOnlineRuntimeSettings(
  body: UpdateFinanzOnlineRuntimeRequest,
  signal?: AbortSignal
): Promise<FinanzOnlineRuntimeSettingsDto> {
  return customInstance<FinanzOnlineRuntimeSettingsDto>({
    url: '/api/admin/finanzonline-outbox/runtime-settings',
    method: 'PUT',
    data: body,
    signal,
  });
}
