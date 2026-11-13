import { customInstance } from '@/lib/axios';

import type { TseBiDashboard, TseBiExportResult } from '../types';

export async function getTseBiDashboard(
  tenantId: string,
  lookbackDays = 30,
  signal?: AbortSignal
): Promise<TseBiDashboard> {
  return customInstance<TseBiDashboard>({
    url: '/api/admin/tse/analytics/dashboard',
    method: 'GET',
    params: { tenantId, lookbackDays },
    signal,
  });
}

export async function exportTseBiReport(
  tenantId: string,
  format: 'csv' | 'pdf',
  lookbackDays = 30
): Promise<TseBiExportResult> {
  return customInstance<TseBiExportResult>({
    url: '/api/admin/tse/analytics/export',
    method: 'POST',
    data: { tenantId, format, lookbackDays },
  });
}

export function downloadBase64File(fileName: string, contentType: string, contentBase64: string) {
  const binary = atob(contentBase64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i += 1) {
    bytes[i] = binary.charCodeAt(i);
  }
  const blob = new Blob([bytes], { type: contentType });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}
