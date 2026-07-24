import { customInstance } from '@/lib/axios';

import type { TseKnowledgeArticle, TseKnowledgeFeedback } from '../types';

export async function searchTseKnowledgeArticles(
  q?: string,
  signal?: AbortSignal
): Promise<TseKnowledgeArticle[]> {
  return customInstance<TseKnowledgeArticle[]>({
    url: '/api/admin/tse/knowledge/search',
    method: 'GET',
    params: q ? { q } : undefined,
    signal,
  });
}

export async function getPopularTseKnowledgeArticles(
  limit = 10,
  signal?: AbortSignal
): Promise<TseKnowledgeArticle[]> {
  return customInstance<TseKnowledgeArticle[]>({
    url: '/api/admin/tse/knowledge/popular',
    method: 'GET',
    params: { limit },
    signal,
  });
}

export async function getTseKnowledgeFaqs(
  signal?: AbortSignal
): Promise<TseKnowledgeArticle[]> {
  return customInstance<TseKnowledgeArticle[]>({
    url: '/api/admin/tse/knowledge/faq',
    method: 'GET',
    signal,
  });
}

export async function getTseKnowledgeArticle(
  articleId: string,
  signal?: AbortSignal
): Promise<TseKnowledgeArticle> {
  return customInstance<TseKnowledgeArticle>({
    url: `/api/admin/tse/knowledge/${articleId}`,
    method: 'GET',
    signal,
  });
}

export async function submitTseKnowledgeFeedback(
  articleId: string,
  rating: number
): Promise<TseKnowledgeFeedback> {
  return customInstance<TseKnowledgeFeedback>({
    url: `/api/admin/tse/knowledge/${articleId}/feedback`,
    method: 'POST',
    data: { rating },
  });
}
