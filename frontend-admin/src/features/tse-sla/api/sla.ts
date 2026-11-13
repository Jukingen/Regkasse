import { customInstance } from '@/lib/axios';

import type { TseSlaAlert, TseSlaReport, TseSlaStatus } from '../types';

export async function getTseSlaReport(
  tenantId: string,
  fromUtc?: string,
  toUtc?: string,
  signal?: AbortSignal
): Promise<TseSlaReport> {
  return customInstance<TseSlaReport>({
    url: '/api/admin/tse/sla/report',
    method: 'GET',
    params: {
      tenantId,
      ...(fromUtc ? { fromUtc } : {}),
      ...(toUtc ? { toUtc } : {}),
    },
    signal,
  });
}

export async function getTseSlaStatus(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseSlaStatus> {
  return customInstance<TseSlaStatus>({
    url: '/api/admin/tse/sla/status',
    method: 'GET',
    params: { tenantId },
    signal,
  });
}

export async function checkTseSlaViolations(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseSlaAlert> {
  return customInstance<TseSlaAlert>({
    url: '/api/admin/tse/sla/check-violations',
    method: 'POST',
    params: { tenantId },
    signal,
  });
}
