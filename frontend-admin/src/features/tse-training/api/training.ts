import { customInstance } from '@/lib/axios';

import type {
  TseTrainingConsoleEntry,
  TseTrainingEnvironment,
  TseTrainingFailureType,
  TseTrainingModule,
  TseTrainingSimulateResult,
} from '../types';

export async function getTseTrainingEnvironment(
  signal?: AbortSignal
): Promise<TseTrainingEnvironment> {
  return customInstance<TseTrainingEnvironment>({
    url: '/api/admin/tse/training',
    method: 'GET',
    signal,
  });
}

export async function startTseTrainingModule(
  moduleId: string
): Promise<TseTrainingModule> {
  return customInstance<TseTrainingModule>({
    url: `/api/admin/tse/training/modules/${encodeURIComponent(moduleId)}/start`,
    method: 'POST',
  });
}

export async function getTseTrainingConsole(
  take = 100,
  signal?: AbortSignal
): Promise<TseTrainingConsoleEntry[]> {
  return customInstance<TseTrainingConsoleEntry[]>({
    url: '/api/admin/tse/training/console',
    method: 'GET',
    params: { take },
    signal,
  });
}

export async function clearTseTrainingConsole(): Promise<void> {
  await customInstance<void>({
    url: '/api/admin/tse/training/console',
    method: 'DELETE',
  });
}

export async function simulateTseTrainingFailure(
  deviceId: string,
  failureType: TseTrainingFailureType | string
): Promise<TseTrainingSimulateResult> {
  return customInstance<TseTrainingSimulateResult>({
    url: '/api/admin/tse/training/simulate',
    method: 'POST',
    data: { deviceId, failureType },
  });
}

export async function resetTseTrainingSimulation(
  deviceId: string
): Promise<TseTrainingSimulateResult> {
  return customInstance<TseTrainingSimulateResult>({
    url: '/api/admin/tse/training/reset',
    method: 'POST',
    data: { deviceId },
  });
}
