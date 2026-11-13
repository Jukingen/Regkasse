import { customInstance } from '@/lib/axios';

import type {
  ConfigureTseHealingRequest,
  TseHealingConfiguration,
  TseHealingReport,
  TseHealingResult,
} from '../types';

export async function getTseHealingConfiguration(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseHealingConfiguration> {
  return customInstance<TseHealingConfiguration>({
    url: '/api/admin/tse/auto-healing/configuration',
    method: 'GET',
    params: { tenantId },
    signal,
  });
}

export async function configureTseHealing(
  tenantId: string,
  body: ConfigureTseHealingRequest
): Promise<TseHealingConfiguration> {
  return customInstance<TseHealingConfiguration>({
    url: '/api/admin/tse/auto-healing/configuration',
    method: 'PUT',
    params: { tenantId },
    data: body,
  });
}

export async function diagnoseAndHealTseDevice(
  deviceId: string
): Promise<TseHealingResult> {
  return customInstance<TseHealingResult>({
    url: `/api/admin/tse/auto-healing/diagnose/${deviceId}`,
    method: 'POST',
  });
}

export async function getTseHealingHistory(
  tenantId: string,
  take = 50,
  signal?: AbortSignal
): Promise<TseHealingReport> {
  return customInstance<TseHealingReport>({
    url: '/api/admin/tse/auto-healing/history',
    method: 'GET',
    params: { tenantId, take },
    signal,
  });
}
