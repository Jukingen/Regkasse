import { customInstance } from '@/lib/axios';

import type {
  TseSustainabilityOptimizationResult,
  TseSustainabilityReport,
} from '../types';

export async function getTseSustainabilityReport(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseSustainabilityReport> {
  return customInstance<TseSustainabilityReport>({
    url: '/api/admin/tse/sustainability/report',
    method: 'GET',
    params: { tenantId },
    signal,
  });
}

export async function getTseSustainabilityOptimizations(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseSustainabilityOptimizationResult> {
  return customInstance<TseSustainabilityOptimizationResult>({
    url: '/api/admin/tse/sustainability/optimizations',
    method: 'GET',
    params: { tenantId },
    signal,
  });
}
