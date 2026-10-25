'use client';

import { useQuery } from '@tanstack/react-query';

import { getWalStatus, getWalStatusQueryKey } from '@/features/backup/logic/walStatusApi';

/** Live WAL health poll. React Query owns the timer. */
export const WAL_STATUS_REFETCH_MS = 30000;

export function useWalStatus(enabled: boolean) {
  return useQuery({
    queryKey: getWalStatusQueryKey(),
    queryFn: getWalStatus,
    enabled,
    refetchInterval: WAL_STATUS_REFETCH_MS,
    refetchIntervalInBackground: true,
    retry: false,
  });
}
