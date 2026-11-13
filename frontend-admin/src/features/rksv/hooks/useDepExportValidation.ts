'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { AXIOS_INSTANCE } from '@/lib/axios';
import { depExportHistoryQueryKey } from '@/features/rksv/hooks/useDepExportHistory';

export type DepExportValidationCheckDto = {
  name: string;
  passed: boolean;
  details: string;
};

export type DepExportHistoryValidationResultDto = {
  exportId: string;
  tenantId?: string;
  isValid: boolean;
  checks: DepExportValidationCheckDto[];
  validatedAt?: string | null;
  errorMessage?: string | null;
};

export type DepExportValidationSummaryItemDto = {
  exportId: string;
  cashRegisterId: string;
  fileName: string;
  exportedAt: string;
  validationStatus?: string | null;
  validatedAt?: string | null;
  isValid?: boolean | null;
};

export type DepExportValidationReportDto = {
  tenantId: string;
  generatedAtUtc: string;
  totalExports: number;
  passedCount: number;
  failedCount: number;
  pendingCount: number;
  skippedCount: number;
  allValidatedPassed: boolean;
  recent: DepExportValidationSummaryItemDto[];
};

export const depExportValidationQueryKeys = {
  all: ['rksv', 'dep-export-validation'] as const,
  report: ['rksv', 'dep-export-validation', 'report'] as const,
  history: (exportId: string) => ['rksv', 'dep-export-validation', 'history', exportId] as const,
};

export async function fetchDepExportValidationReport(): Promise<DepExportValidationReportDto> {
  const response = await AXIOS_INSTANCE.get<DepExportValidationReportDto>(
    '/api/admin/rksv/dep-export/validation-report'
  );
  return response.data;
}

export async function fetchDepExportHistoryValidation(
  exportId: string
): Promise<DepExportHistoryValidationResultDto> {
  const response = await AXIOS_INSTANCE.get<DepExportHistoryValidationResultDto>(
    `/api/admin/rksv/dep-export/history/${exportId}/validation`
  );
  return normalizeValidationResult(response.data);
}

export async function runDepExportHistoryValidation(
  exportId: string
): Promise<DepExportHistoryValidationResultDto> {
  const response = await AXIOS_INSTANCE.post<DepExportHistoryValidationResultDto>(
    `/api/admin/rksv/dep-export/history/${exportId}/validate`
  );
  return normalizeValidationResult(response.data);
}

function normalizeValidationResult(
  data: DepExportHistoryValidationResultDto
): DepExportHistoryValidationResultDto {
  return {
    ...data,
    checks: (data.checks ?? []).map((check) => ({
      name: check.name ?? '',
      passed: Boolean(check.passed),
      details: check.details ?? '',
    })),
  };
}

export function useDepExportValidationReport() {
  return useQuery({
    queryKey: depExportValidationQueryKeys.report,
    queryFn: fetchDepExportValidationReport,
    staleTime: 30_000,
  });
}

export function useDepExportHistoryValidation(exportId: string | null | undefined) {
  return useQuery({
    queryKey: depExportValidationQueryKeys.history(exportId ?? ''),
    queryFn: () => fetchDepExportHistoryValidation(exportId!),
    enabled: Boolean(exportId),
    staleTime: 15_000,
  });
}

export function useRunDepExportValidation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (exportId: string) => runDepExportHistoryValidation(exportId),
    onSuccess: async (result) => {
      queryClient.setQueryData(depExportValidationQueryKeys.history(result.exportId), result);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: depExportValidationQueryKeys.all }),
        queryClient.invalidateQueries({ queryKey: depExportHistoryQueryKey() }),
      ]);
    },
  });
}

export function resolveValidationBadgeStatus(
  status: string | null | undefined,
  isValid?: boolean | null
): 'success' | 'error' | 'warning' | 'default' | 'processing' {
  if (status === 'Passed' || isValid === true) return 'success';
  if (status === 'Failed' || isValid === false) return 'error';
  if (status === 'Pending') return 'processing';
  if (status === 'Skipped') return 'default';
  return 'warning';
}
