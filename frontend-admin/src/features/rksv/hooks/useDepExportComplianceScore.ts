'use client';

import { useQuery } from '@tanstack/react-query';

import { AXIOS_INSTANCE } from '@/lib/axios';

export type DepExportScoreFactorDto = {
  name: string;
  weight: number;
  score: number;
  status: 'Passed' | 'Warning' | 'Failed' | string;
  description: string;
};

export type DepExportComplianceScoreDto = {
  tenantId: string;
  score: number;
  grade: string;
  calculatedAt: string;
  factors: DepExportScoreFactorDto[];
  criticalIssues: string[];
  warnings: string[];
  disclaimer?: string;
};

export type DepExportComplianceScoreHistoryItemDto = {
  id: string;
  score: number;
  grade: string;
  calculatedAt: string;
};

export type DepExportComplianceScoreHistoryDto = {
  tenantId: string;
  items: DepExportComplianceScoreHistoryItemDto[];
};

export type DepExportImprovementSuggestionDto = {
  code: string;
  severity: string;
  title: string;
  description: string;
  deepLink?: string | null;
};

export const depExportComplianceScoreQueryKeys = {
  all: ['rksv', 'dep-export-compliance-score'] as const,
  score: ['rksv', 'dep-export-compliance-score', 'current'] as const,
  history: ['rksv', 'dep-export-compliance-score', 'history'] as const,
  suggestions: ['rksv', 'dep-export-compliance-score', 'suggestions'] as const,
};

export async function fetchDepExportComplianceScore(): Promise<DepExportComplianceScoreDto> {
  const response = await AXIOS_INSTANCE.get<DepExportComplianceScoreDto>(
    '/api/admin/rksv/dep-export/compliance/score'
  );
  return {
    ...response.data,
    factors: response.data.factors ?? [],
    criticalIssues: response.data.criticalIssues ?? [],
    warnings: response.data.warnings ?? [],
  };
}

export async function fetchDepExportComplianceScoreHistory(): Promise<DepExportComplianceScoreHistoryDto> {
  const response = await AXIOS_INSTANCE.get<DepExportComplianceScoreHistoryDto>(
    '/api/admin/rksv/dep-export/compliance/score/history'
  );
  return {
    ...response.data,
    items: response.data.items ?? [],
  };
}

export async function fetchDepExportImprovementSuggestions(): Promise<
  DepExportImprovementSuggestionDto[]
> {
  const response = await AXIOS_INSTANCE.get<DepExportImprovementSuggestionDto[]>(
    '/api/admin/rksv/dep-export/compliance/score/suggestions'
  );
  return response.data ?? [];
}

export function useDepExportComplianceScore() {
  return useQuery({
    queryKey: depExportComplianceScoreQueryKeys.score,
    queryFn: fetchDepExportComplianceScore,
    staleTime: 30_000,
  });
}

export function useDepExportComplianceScoreHistory() {
  return useQuery({
    queryKey: depExportComplianceScoreQueryKeys.history,
    queryFn: fetchDepExportComplianceScoreHistory,
    staleTime: 60_000,
  });
}

export function scoreColor(score: number): string {
  if (score >= 80) return '#389e0d';
  if (score >= 60) return '#d48806';
  return '#cf1322';
}

export function gradeTagColor(grade: string): string {
  switch (grade) {
    case 'A':
      return 'success';
    case 'B':
      return 'processing';
    case 'C':
      return 'warning';
    case 'D':
      return 'orange';
    default:
      return 'error';
  }
}

export function factorStrokeColor(status: string): string {
  if (status === 'Passed') return '#52c41a';
  if (status === 'Warning') return '#faad14';
  return '#cf1322';
}
