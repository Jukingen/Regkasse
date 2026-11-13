import { customInstance } from '@/lib/axios';

import type { TseComplianceDashboard } from '../types';

export async function getTseComplianceDashboard(
  tenantId: string,
  fromUtc?: string,
  toUtc?: string,
  signal?: AbortSignal
): Promise<TseComplianceDashboard> {
  return customInstance<TseComplianceDashboard>({
    url: '/api/admin/tse/compliance/dashboard',
    method: 'GET',
    params: {
      tenantId,
      ...(fromUtc ? { fromUtc } : {}),
      ...(toUtc ? { toUtc } : {}),
    },
    signal,
  });
}

/** Downloads JSON compliance export (browser save). */
export async function exportTseComplianceReport(
  tenantId: string,
  fromUtc?: string,
  toUtc?: string
): Promise<void> {
  const blob = await customInstance<Blob>({
    url: '/api/admin/tse/compliance/export',
    method: 'GET',
    params: {
      tenantId,
      ...(fromUtc ? { fromUtc } : {}),
      ...(toUtc ? { toUtc } : {}),
    },
    responseType: 'blob',
  });

  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = `tse-compliance-${tenantId}.json`;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}
