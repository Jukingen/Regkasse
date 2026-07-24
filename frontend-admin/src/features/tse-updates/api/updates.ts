import { customInstance } from '@/lib/axios';

import type { TseUpdateHistory, TseUpdateResult, TseUpdateStatus } from '../types';

export async function getTseUpdateStatus(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseUpdateStatus> {
  return customInstance<TseUpdateStatus>({
    url: '/api/admin/tse/updates/status',
    method: 'GET',
    params: { tenantId },
    signal,
  });
}

export async function applyTseUpdate(
  tenantId: string,
  updateType: string
): Promise<TseUpdateResult> {
  return customInstance<TseUpdateResult>({
    url: '/api/admin/tse/updates/apply',
    method: 'POST',
    params: { tenantId },
    data: { updateType },
  });
}

export async function getTseUpdateHistory(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseUpdateHistory> {
  return customInstance<TseUpdateHistory>({
    url: '/api/admin/tse/updates/history',
    method: 'GET',
    params: { tenantId },
    signal,
  });
}
