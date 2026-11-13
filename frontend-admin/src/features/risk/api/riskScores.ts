import type { RiskScoreDto, RiskScoreListResponse } from '@/features/risk/types';
import { customInstance } from '@/lib/axios';

export async function getRiskScores(
  params?: { unresolvedOnly?: boolean; riskLevel?: string; limit?: number; offset?: number },
  signal?: AbortSignal
): Promise<RiskScoreListResponse> {
  return customInstance<RiskScoreListResponse>({
    url: '/api/admin/risk',
    method: 'GET',
    params: {
      unresolvedOnly: params?.unresolvedOnly ?? true,
      riskLevel: params?.riskLevel,
      limit: params?.limit ?? 100,
      offset: params?.offset ?? 0,
    },
    signal,
  });
}

export async function resolveRisk(
  id: string,
  resolution: string,
  signal?: AbortSignal
): Promise<RiskScoreDto> {
  return customInstance<RiskScoreDto>({
    url: `/api/admin/risk/${id}/resolve`,
    method: 'POST',
    data: { resolution },
    signal,
  });
}
