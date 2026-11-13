import { customInstance } from '@/lib/axios';

import type {
  TseLogAggregationResult,
  TseLogAnalysisReport,
  TseLogSearchResult,
} from '../types';

export async function aggregateTseLogs(
  tenantId: string,
  fromUtc?: string,
  toUtc?: string,
  signal?: AbortSignal
): Promise<TseLogAggregationResult> {
  return customInstance<TseLogAggregationResult>({
    url: '/api/admin/tse/logs/aggregate',
    method: 'GET',
    params: {
      tenantId,
      ...(fromUtc ? { fromUtc } : {}),
      ...(toUtc ? { toUtc } : {}),
    },
    signal,
  });
}

export async function searchTseLogs(
  params: {
    tenantId: string;
    fromUtc?: string;
    toUtc?: string;
    query?: string;
    level?: string;
    provider?: string;
    source?: string;
    skip?: number;
    take?: number;
  },
  signal?: AbortSignal
): Promise<TseLogSearchResult> {
  return customInstance<TseLogSearchResult>({
    url: '/api/admin/tse/logs/search',
    method: 'GET',
    params,
    signal,
  });
}

export async function analyzeTseLogs(
  tenantId: string,
  body?: { fromUtc?: string; toUtc?: string; focusLevel?: string },
  signal?: AbortSignal
): Promise<TseLogAnalysisReport> {
  return customInstance<TseLogAnalysisReport>({
    url: '/api/admin/tse/logs/analyze',
    method: 'POST',
    params: { tenantId },
    data: body ?? {},
    signal,
  });
}
