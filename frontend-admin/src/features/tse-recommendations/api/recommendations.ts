import { customInstance } from '@/lib/axios';

import type {
  TseRecommendation,
  TseRecommendationFeedback,
  TseRecommendationResult,
} from '../types';

export async function getTseRecommendations(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseRecommendation[]> {
  return customInstance<TseRecommendation[]>({
    url: '/api/admin/tse/recommendations',
    method: 'GET',
    params: { tenantId },
    signal,
  });
}

export async function applyTseRecommendation(
  recommendationId: string
): Promise<TseRecommendationResult> {
  return customInstance<TseRecommendationResult>({
    url: `/api/admin/tse/recommendations/${encodeURIComponent(recommendationId)}/apply`,
    method: 'POST',
  });
}

export async function dismissTseRecommendation(
  recommendationId: string
): Promise<TseRecommendationResult> {
  return customInstance<TseRecommendationResult>({
    url: `/api/admin/tse/recommendations/${encodeURIComponent(recommendationId)}/dismiss`,
    method: 'POST',
  });
}

export async function rateTseRecommendation(
  recommendationId: string,
  rating: number
): Promise<TseRecommendationFeedback> {
  return customInstance<TseRecommendationFeedback>({
    url: `/api/admin/tse/recommendations/${encodeURIComponent(recommendationId)}/rate`,
    method: 'POST',
    data: { rating },
  });
}
