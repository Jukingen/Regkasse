/**
 * GET /api/tse/health — cached TSE operational status (backend background probe).
 */
import { apiClient } from './config';

export type TseOperationalHealthStatus = 'Online' | 'Degraded' | 'Offline';

export interface TseHealthApiResponse {
  status: TseOperationalHealthStatus | string;
  lastCheckUtc?: string | null;
  lastSuccessfulPingUtc?: string | null;
  consecutiveFailures: number;
  estimatedRecoveryTimeUtc?: string | null;
  lastErrorMessageSafe?: string | null;
  nonFiscalPendingQueueCount?: number | null;
}

export async function fetchTseHealth(cashRegisterId?: string | null): Promise<{
  body: TseHealthApiResponse;
  latencyMs: number;
}> {
  const started = typeof performance !== 'undefined' ? performance.now() : Date.now();
  const qs =
    cashRegisterId && cashRegisterId !== '00000000-0000-0000-0000-000000000000'
      ? `?cashRegisterId=${encodeURIComponent(cashRegisterId)}`
      : '';
  const body = await apiClient.get<TseHealthApiResponse>(`/tse/health${qs}`);
  const ended = typeof performance !== 'undefined' ? performance.now() : Date.now();
  return { body, latencyMs: Math.max(0, ended - started) };
}
