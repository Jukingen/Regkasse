import { customInstance } from '@/lib/axios';

export type TseOfflineQueueRegisterSummary = {
  cashRegisterId: string;
  registerNumber?: string | null;
  queuedCount: number;
  maxPerRegister: number;
  isAtCap: boolean;
  isNearCap: boolean;
};

export type TseOfflineQueueStatus = {
  tenantId: string;
  totalQueued: number;
  criticalThreshold: number;
  warningThreshold: number;
  maxPerRegister: number;
  isCritical: boolean;
  isWarning: boolean;
  oldestTransaction?: string | null;
  newestTransaction?: string | null;
  byRegister: TseOfflineQueueRegisterSummary[];
};

export async function getTseOfflineQueueStatus(
  tenantId?: string,
  signal?: AbortSignal
): Promise<TseOfflineQueueStatus> {
  return customInstance<TseOfflineQueueStatus>({
    url: '/api/admin/tse/offline-queue/status',
    method: 'GET',
    params: tenantId ? { tenantId } : undefined,
    signal,
  });
}
