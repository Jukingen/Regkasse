import { useQuery } from '@tanstack/react-query';
import { useMemo } from 'react';

import { usePollRestoreVerificationDashboardInterval } from '@/features/backup-dr/logic/backupDashboardQueryTiming';
import {
  getRestoreVerificationLatest,
  getRestoreVerificationLatestQueryKey,
  getRestoreVerificationRuns,
  getRestoreVerificationRunsQueryKey,
} from '@/features/backup/logic/restoreVerificationApi';
import { buildRestoreVerificationProgressViewModel } from '@/features/backup/logic/restoreVerificationProgressPresentation';
import { restoreVerificationStatusValue } from '@/features/backup/logic/restoreVerificationPresentation';

export function useRestoreVerificationProgress(enabled = true) {
  const pollLatest = usePollRestoreVerificationDashboardInterval();

  const latestQuery = useQuery({
    queryKey: getRestoreVerificationLatestQueryKey(),
    queryFn: getRestoreVerificationLatest,
    enabled,
    refetchInterval: pollLatest,
    refetchOnWindowFocus: true,
  });

  const recentQuery = useQuery({
    queryKey: getRestoreVerificationRunsQueryKey({ page: 1, pageSize: 5, status: 2 }),
    queryFn: () => getRestoreVerificationRuns({ page: 1, pageSize: 5, status: 2 }),
    enabled,
    staleTime: 30_000,
  });

  const typicalDurationMs = useMemo(() => {
    const durations = (recentQuery.data?.items ?? [])
      .map((row) => row.durationMs)
      .filter((n): n is number => typeof n === 'number' && n > 0);
    if (durations.length === 0) return null;
    return Math.round(durations.reduce((a, b) => a + b, 0) / durations.length);
  }, [recentQuery.data?.items]);

  const progress = useMemo(
    () => buildRestoreVerificationProgressViewModel(latestQuery.data, { typicalDurationMs }),
    [latestQuery.data, typicalDurationMs]
  );

  const status = restoreVerificationStatusValue(latestQuery.data?.status);

  return {
    progress,
    isLoading: latestQuery.isLoading,
    isError: latestQuery.isError,
    isInProgress: status === 0 || status === 1,
  };
}
