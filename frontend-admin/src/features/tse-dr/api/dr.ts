import { customInstance } from '@/lib/axios';

import type { TseDrReport, TseDrRunbook, TseDrStatus } from '../types';

export async function getTseDrStatus(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseDrStatus> {
  return customInstance<TseDrStatus>({
    url: '/api/admin/tse/disaster-recovery/status',
    method: 'GET',
    params: { tenantId },
    signal,
  });
}

export async function generateTseDrRunbook(
  tenantId: string,
  scenario = 'TSEFailure',
  signal?: AbortSignal
): Promise<TseDrRunbook> {
  return customInstance<TseDrRunbook>({
    url: '/api/admin/tse/disaster-recovery/runbooks',
    method: 'POST',
    params: { tenantId },
    data: { scenario },
    signal,
  });
}

export async function runTseDrDrill(
  tenantId: string,
  scenario = 'TSEFailure',
  signal?: AbortSignal
): Promise<TseDrReport> {
  return customInstance<TseDrReport>({
    url: '/api/admin/tse/disaster-recovery/drill',
    method: 'POST',
    params: { tenantId, scenario },
    signal,
  });
}
