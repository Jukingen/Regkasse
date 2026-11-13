'use client';

import { useQuery } from '@tanstack/react-query';
import { useEffect } from 'react';

import {
  type BackupComplianceStatusResponseDto,
  getBackupComplianceStatus,
  getBackupComplianceStatusQueryKey,
} from '@/features/backup/logic/backupComplianceStatusApi';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { normalizeApiError } from '@/shared/errors/normalizedApiError';

export type UseComplianceStatusOptions = {
  enabled?: boolean;
};

/** RKSV product-gate rollup — GET /api/admin/backup/compliance-status. */
export function useComplianceStatus(options?: UseComplianceStatusOptions) {
  const enabled = options?.enabled !== false;

  const query = useQuery({
    queryKey: getBackupComplianceStatusQueryKey(),
    queryFn: getBackupComplianceStatus,
    enabled,
    staleTime: 30_000,
    refetchOnWindowFocus: true,
  });

  const data: BackupComplianceStatusResponseDto | null = query.data ?? null;

  useEffect(() => {
    if (!query.isError || !query.error) return;
    const normalized = normalizeApiError(query.error);
    technicalConsole.warn('Backup compliance-status query failed', {
      httpStatus: normalized.httpStatus,
      code: normalized.code,
      message: normalized.rawMessage,
    });
  }, [query.isError, query.error]);

  return {
    data,
    isLoading: query.isLoading && !query.data,
    isFetching: query.isFetching,
    isError: query.isError,
    error: query.error,
    refetch: query.refetch,
  };
}
