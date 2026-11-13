'use client';

import { useQuery } from '@tanstack/react-query';

import {
  type BackupStorageCostResponseDto,
  getBackupStorageCosts,
  getBackupStorageCostsQueryKey,
} from '@/features/backup/logic/backupStorageCostsApi';

export type UseStorageCostsOptions = {
  enabled?: boolean;
};

/** Indicative storage cost rollup — GET /api/admin/backup/storage-costs. */
export function useStorageCosts(options?: UseStorageCostsOptions) {
  const enabled = options?.enabled !== false;

  const query = useQuery({
    queryKey: getBackupStorageCostsQueryKey(),
    queryFn: getBackupStorageCosts,
    enabled,
    staleTime: 30_000,
    refetchOnWindowFocus: true,
  });

  const data: BackupStorageCostResponseDto | null = query.data ?? null;

  return {
    data,
    isLoading: query.isLoading && !query.data,
    isFetching: query.isFetching,
    isError: query.isError,
    refetch: query.refetch,
  };
}
