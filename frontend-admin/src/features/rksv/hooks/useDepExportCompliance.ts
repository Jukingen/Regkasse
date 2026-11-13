'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { AXIOS_INSTANCE } from '@/lib/axios';
import type { DepExportRequestParams, RksvDepExportRoot } from '@/features/rksv/types/depExport';

export type DepExportRequirementDto = {
  id: string;
  tenantId: string;
  requirementType: string;
  title: string;
  description: string;
  dueDate?: string | null;
  isCompleted: boolean;
  priority: number;
  category: string;
  periodStart?: string | null;
  periodEnd?: string | null;
};

export type DepExportCompliancePeriodDto = {
  id: string;
  tenantId: string;
  periodType: string;
  periodStart: string;
  periodEnd: string;
  status: string;
  exportedAt?: string | null;
  exportedBy?: string | null;
  fileName?: string | null;
  fileHash?: string | null;
  historyId?: string | null;
  createdAt: string;
  updatedAt?: string | null;
};

export type DepExportComplianceStatusDto = {
  tenantId: string;
  isCompliant: boolean;
  totalRequirements: number;
  completedCount: number;
  pendingCount: number;
  overdueCount: number;
  legalIncompleteCount: number;
  nextRequirement?: DepExportRequirementDto | null;
  currentPeriod?: DepExportCompliancePeriodDto | null;
  checkedAtUtc: string;
  disclaimer: string;
};

export const depExportComplianceQueryKeys = {
  all: ['rksv', 'dep-export-compliance'] as const,
  status: ['rksv', 'dep-export-compliance', 'status'] as const,
  requirements: ['rksv', 'dep-export-compliance', 'requirements'] as const,
  currentPeriod: ['rksv', 'dep-export-compliance', 'current-period'] as const,
};

export async function fetchDepExportComplianceStatus(): Promise<DepExportComplianceStatusDto> {
  const response = await AXIOS_INSTANCE.get<DepExportComplianceStatusDto>(
    '/api/admin/rksv/dep-export/compliance'
  );
  return response.data;
}

export async function fetchDepExportRequirements(): Promise<DepExportRequirementDto[]> {
  const response = await AXIOS_INSTANCE.get<DepExportRequirementDto[]>(
    '/api/admin/rksv/dep-export/requirements'
  );
  return response.data;
}

export async function fetchDepExportCurrentPeriod(): Promise<DepExportCompliancePeriodDto | null> {
  const response = await AXIOS_INSTANCE.get<DepExportCompliancePeriodDto | ''>(
    '/api/admin/rksv/dep-export/compliance/current-period',
    { validateStatus: (status) => status === 200 || status === 204 }
  );
  if (response.status === 204 || !response.data) return null;
  return response.data as DepExportCompliancePeriodDto;
}

export function useDepExportComplianceStatus() {
  return useQuery({
    queryKey: depExportComplianceQueryKeys.status,
    queryFn: fetchDepExportComplianceStatus,
    staleTime: 30_000,
  });
}

export function useDepExportRequirements() {
  return useQuery({
    queryKey: depExportComplianceQueryKeys.requirements,
    queryFn: fetchDepExportRequirements,
    staleTime: 30_000,
  });
}

export function useGenerateDepExportForCompliance() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (params: DepExportRequestParams): Promise<RksvDepExportRoot> => {
      const response = await AXIOS_INSTANCE.get<RksvDepExportRoot>('/api/admin/rksv/dep-export', {
        params,
      });
      return response.data;
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: depExportComplianceQueryKeys.all });
    },
  });
}

export function computeComplianceScore(status: DepExportComplianceStatusDto | undefined): number {
  if (!status) return 0;
  if (status.totalRequirements <= 0) return status.isCompliant ? 100 : 0;
  return Math.round((status.completedCount / status.totalRequirements) * 100);
}
