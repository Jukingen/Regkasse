import { customInstance } from '@/lib/axios';

import type { TseAnomaly, TseAnomalyDashboard, TseAnomalyResult } from '../types';

export async function getTseAnomalyDashboard(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseAnomalyDashboard> {
  return customInstance<TseAnomalyDashboard>({
    url: '/api/admin/tse/anomalies/dashboard',
    method: 'GET',
    params: { tenantId },
    signal,
  });
}

export async function detectTseAnomalies(tenantId: string): Promise<TseAnomalyResult> {
  return customInstance<TseAnomalyResult>({
    url: '/api/admin/tse/anomalies/detect',
    method: 'POST',
    params: { tenantId },
  });
}

export async function resolveTseAnomaly(anomalyId: string): Promise<TseAnomaly> {
  return customInstance<TseAnomaly>({
    url: `/api/admin/tse/anomalies/${anomalyId}/resolve`,
    method: 'POST',
  });
}
