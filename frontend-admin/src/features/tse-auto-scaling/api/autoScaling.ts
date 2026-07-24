import { customInstance } from '@/lib/axios';

import type {
  TseScalingHistory,
  TseScalingPolicy,
  TseScalingResult,
  TseScalingStatus,
} from '../types';

export async function getTseScalingStatus(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseScalingStatus> {
  return customInstance<TseScalingStatus>({
    url: '/api/admin/tse/auto-scaling/status',
    method: 'GET',
    params: { tenantId },
    signal,
  });
}

export async function getTseScalingHistory(
  tenantId: string,
  take = 50,
  signal?: AbortSignal
): Promise<TseScalingHistory> {
  return customInstance<TseScalingHistory>({
    url: '/api/admin/tse/auto-scaling/history',
    method: 'GET',
    params: { tenantId, take },
    signal,
  });
}

export async function configureTseScalingPolicy(
  tenantId: string,
  policy: Omit<TseScalingPolicy, 'tenantId' | 'updatedAt'>
): Promise<TseScalingPolicy> {
  return customInstance<TseScalingPolicy>({
    url: '/api/admin/tse/auto-scaling/policy',
    method: 'PUT',
    params: { tenantId },
    data: policy,
  });
}

export async function triggerTseScaling(tenantId: string): Promise<TseScalingResult> {
  return customInstance<TseScalingResult>({
    url: '/api/admin/tse/auto-scaling/evaluate',
    method: 'POST',
    params: { tenantId },
  });
}
