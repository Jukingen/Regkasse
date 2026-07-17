"use client";

import { useQuery } from "@tanstack/react-query";
import {
  getManualRestoreHistory,
  MANUAL_RESTORE_HISTORY_PATH,
  type RestoreRequestHistoryResponseDto,
} from "@/features/backup-dr/logic/manualRestoreApi";

export type UseRestoreHistoryOptions = {
  enabled?: boolean;
  page?: number;
  pageSize?: number;
};

/**
 * Paginated Super Admin manual restore request history
 * (GET /api/admin/restore/history — not a fictional RestoreHistory table).
 */
export function useRestoreHistory(options?: UseRestoreHistoryOptions) {
  const page = options?.page ?? 1;
  const pageSize = options?.pageSize ?? 20;
  const enabled = options?.enabled !== false;

  const query = useQuery({
    queryKey: [MANUAL_RESTORE_HISTORY_PATH, page, pageSize],
    queryFn: () => getManualRestoreHistory(page, pageSize),
    enabled,
    staleTime: 15_000,
  });

  const data: RestoreRequestHistoryResponseDto | null = query.data ?? null;

  return {
    data,
    items: data?.items ?? [],
    totalCount: data?.totalCount ?? 0,
    page: data?.page ?? page,
    pageSize: data?.pageSize ?? pageSize,
    isLoading: query.isLoading && !query.data,
    isFetching: query.isFetching,
    isError: query.isError,
    refetch: query.refetch,
  };
}
