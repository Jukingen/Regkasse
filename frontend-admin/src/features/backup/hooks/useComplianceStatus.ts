"use client";

import { useQuery } from "@tanstack/react-query";
import {
  getBackupComplianceStatus,
  getBackupComplianceStatusQueryKey,
  type BackupComplianceStatusResponseDto,
} from "@/features/backup/logic/backupComplianceStatusApi";

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

  return {
    data,
    isLoading: query.isLoading && !query.data,
    isFetching: query.isFetching,
    isError: query.isError,
    refetch: query.refetch,
  };
}
