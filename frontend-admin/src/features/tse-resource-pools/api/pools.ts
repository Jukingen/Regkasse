import { customInstance } from '@/lib/axios';

import type {
  AssignTenantToTsePoolRequest,
  CreateTseResourcePoolRequest,
  TsePoolAssignmentResult,
  TsePoolMetrics,
  TsePoolStatus,
  TseResourcePool,
} from '../types';

export async function listTseResourcePools(signal?: AbortSignal): Promise<TseResourcePool[]> {
  return customInstance<TseResourcePool[]>({
    url: '/api/admin/tse/resource-pools',
    method: 'GET',
    signal,
  });
}

export async function createTseResourcePool(
  body: CreateTseResourcePoolRequest,
  signal?: AbortSignal
): Promise<TseResourcePool> {
  return customInstance<TseResourcePool>({
    url: '/api/admin/tse/resource-pools',
    method: 'POST',
    data: body,
    signal,
  });
}

export async function getTseResourcePool(
  poolId: string,
  signal?: AbortSignal
): Promise<TseResourcePool> {
  return customInstance<TseResourcePool>({
    url: `/api/admin/tse/resource-pools/${poolId}`,
    method: 'GET',
    signal,
  });
}

export async function getTsePoolStatus(
  poolId: string,
  signal?: AbortSignal
): Promise<TsePoolStatus> {
  return customInstance<TsePoolStatus>({
    url: `/api/admin/tse/resource-pools/${poolId}/status`,
    method: 'GET',
    signal,
  });
}

export async function getTsePoolMetrics(
  poolId: string,
  signal?: AbortSignal
): Promise<TsePoolMetrics> {
  return customInstance<TsePoolMetrics>({
    url: `/api/admin/tse/resource-pools/${poolId}/metrics`,
    method: 'GET',
    signal,
  });
}

export async function assignTenantToTsePool(
  body: AssignTenantToTsePoolRequest,
  signal?: AbortSignal
): Promise<TsePoolAssignmentResult> {
  return customInstance<TsePoolAssignmentResult>({
    url: '/api/admin/tse/resource-pools/assign',
    method: 'POST',
    data: body,
    signal,
  });
}

export async function unassignTenantFromTsePool(
  tenantId: string,
  signal?: AbortSignal
): Promise<TsePoolAssignmentResult> {
  return customInstance<TsePoolAssignmentResult>({
    url: '/api/admin/tse/resource-pools/unassign',
    method: 'POST',
    data: { tenantId, poolId: '00000000-0000-0000-0000-000000000000' },
    signal,
  });
}
