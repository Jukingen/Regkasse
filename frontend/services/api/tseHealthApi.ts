/**
 * GET /api/pos/tse/status — cashier TSE indicator (Active / Degraded / Inactive).
 * GET /api/tse/health remains for the process probe; POS should use the pos prefix.
 */
import { apiClient } from './config';
import {
  toOperationalHealthFromPosTse,
  type PosTseIndicatorStatus,
  type TseOperationalHealthStatus,
} from '../../utils/posTseStatus';

export type { PosTseIndicatorStatus, TseOperationalHealthStatus };
export { toOperationalHealthFromPosTse };

export interface TseHealthApiResponse {
  status: TseOperationalHealthStatus | string;
  lastCheckUtc?: string | null;
  lastSuccessfulPingUtc?: string | null;
  consecutiveFailures: number;
  estimatedRecoveryTimeUtc?: string | null;
  lastErrorMessageSafe?: string | null;
  nonFiscalPendingQueueCount?: number | null;
}

export interface PosTseStatusApiResponse {
  status: PosTseIndicatorStatus | string;
  message?: string | null;
  lastCheck?: string | null;
  scuId?: string | null;
  tssId?: string | null;
  certificateValidUntil?: string | null;
  cached?: boolean;
  operationalHealth?: TseOperationalHealthStatus | string;
  lastErrorMessageSafe?: string | null;
  nonFiscalPendingQueueCount?: number | null;
  estimatedRecoveryTimeUtc?: string | null;
  lastSuccessfulPingUtc?: string | null;
  environment?: string | null;
}

function cashRegisterQuery(cashRegisterId?: string | null): string {
  return cashRegisterId && cashRegisterId !== '00000000-0000-0000-0000-000000000000'
    ? `?cashRegisterId=${encodeURIComponent(cashRegisterId)}`
    : '';
}

export async function fetchPosTseStatus(cashRegisterId?: string | null): Promise<{
  body: PosTseStatusApiResponse;
  latencyMs: number;
}> {
  const started = typeof performance !== 'undefined' ? performance.now() : Date.now();
  const body = await apiClient.get<PosTseStatusApiResponse>(
    `/pos/tse/status${cashRegisterQuery(cashRegisterId)}`
  );
  const ended = typeof performance !== 'undefined' ? performance.now() : Date.now();
  return { body, latencyMs: Math.max(0, ended - started) };
}

/** @deprecated Prefer fetchPosTseStatus for POS. Kept for process-health callers. */
export async function fetchTseHealth(cashRegisterId?: string | null): Promise<{
  body: TseHealthApiResponse;
  latencyMs: number;
}> {
  const started = typeof performance !== 'undefined' ? performance.now() : Date.now();
  const body = await apiClient.get<TseHealthApiResponse>(
    `/tse/health${cashRegisterQuery(cashRegisterId)}`
  );
  const ended = typeof performance !== 'undefined' ? performance.now() : Date.now();
  return { body, latencyMs: Math.max(0, ended - started) };
}
