'use client';

import { useQuery } from '@tanstack/react-query';

import {
  type RestoreComplianceCheckResponseDto,
  getRestoreComplianceCheck,
  getRestoreComplianceCheckQueryKey,
} from '@/features/backup-dr/logic/manualRestoreApi';

export type UseRestoreComplianceCheckOptions = {
  enabled?: boolean;
  /** Operating tenant for same-tenant gate; omit for platform Super Admin. */
  tenantId?: string | null;
};

/**
 * Pre-restore compliance check — GET /api/admin/restore/compliance-check.
 * Never invents success; mirrors backend ComplianceCheckService.
 */
export function useRestoreComplianceCheck(
  backupRunId: string | null | undefined,
  options?: UseRestoreComplianceCheckOptions
) {
  const enabled = options?.enabled !== false && Boolean(backupRunId?.trim());
  const tenantId = options?.tenantId ?? null;

  const query = useQuery({
    queryKey: getRestoreComplianceCheckQueryKey(backupRunId ?? '', tenantId),
    queryFn: () => getRestoreComplianceCheck(backupRunId!.trim(), tenantId),
    enabled,
    staleTime: 30_000,
  });

  const data: RestoreComplianceCheckResponseDto | null = query.data ?? null;

  return {
    data,
    succeeded: data?.succeeded === true,
    isLoading: query.isLoading && !query.data,
    isFetching: query.isFetching,
    isError: query.isError,
    refetch: query.refetch,
  };
}
