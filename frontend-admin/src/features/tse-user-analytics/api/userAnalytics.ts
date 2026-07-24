import { customInstance } from '@/lib/axios';

import type {
  TseCohortAnalysisResult,
  TseFeatureUsageReport,
  TseUserBehaviorReport,
} from '../types';

export async function getTseUserBehaviorReport(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseUserBehaviorReport> {
  return customInstance<TseUserBehaviorReport>({
    url: '/api/admin/tse/user-analytics/report',
    method: 'GET',
    params: { tenantId },
    signal,
  });
}

export async function getTseFeatureUsageReport(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseFeatureUsageReport> {
  return customInstance<TseFeatureUsageReport>({
    url: '/api/admin/tse/user-analytics/features',
    method: 'GET',
    params: { tenantId },
    signal,
  });
}

export async function getTseCohortAnalysis(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseCohortAnalysisResult> {
  return customInstance<TseCohortAnalysisResult>({
    url: '/api/admin/tse/user-analytics/cohorts',
    method: 'GET',
    params: { tenantId },
    signal,
  });
}
