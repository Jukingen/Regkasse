/**
 * Polls GET /api/admin/backup/runs/{id} and maps it to a progress view-model.
 */

import { useMemo } from "react";
import { useGetApiAdminBackupStatusLatest } from "@/api/generated/admin-backup/admin-backup";
import { BackupRunStatus } from "@/api/generated/model/backupRunStatus";
import { useBackupRun } from "@/features/backup/hooks/useBackupRun";
import {
  buildBackupProgressViewModel,
  type BackupProgressViewModel,
} from "@/features/backup/logic/backupProgressPresentation";
import { BACKUP_DASHBOARD_STATS_POLL_MS } from "@/features/backup/logic/backupDashboardStatsApi";

export type UseBackupProgressOptions = {
  /** When backupRunId is null, track the latest in-progress run from status/latest. */
  autoTrackLatestInProgress?: boolean;
  enabled?: boolean;
};

function isInProgress(status: number | undefined): boolean {
  return (
    status === BackupRunStatus.NUMBER_0 ||
    status === BackupRunStatus.NUMBER_1 ||
    status === BackupRunStatus.NUMBER_2
  );
}

export function useBackupProgress(
  backupRunId: string | null | undefined,
  options?: UseBackupProgressOptions,
) {
  const autoTrack = options?.autoTrackLatestInProgress === true;
  const enabled = options?.enabled !== false;

  const statusQuery = useGetApiAdminBackupStatusLatest({
    query: {
      enabled: enabled && (autoTrack || !backupRunId?.trim()),
      refetchInterval: BACKUP_DASHBOARD_STATS_POLL_MS,
      refetchOnWindowFocus: true,
    },
  });

  const latestInProgressId =
    autoTrack && isInProgress(statusQuery.data?.latestRun?.status)
      ? statusQuery.data?.latestRun?.id ?? null
      : null;

  const resolvedId = backupRunId?.trim() || latestInProgressId || null;

  const runQuery = useBackupRun(resolvedId, {
    enabled: enabled && Boolean(resolvedId),
  });

  const progress: BackupProgressViewModel | null = useMemo(
    () =>
      buildBackupProgressViewModel(runQuery.data, {
        averageSucceededDurationSeconds:
          statusQuery.data?.averageSucceededBackupDurationSeconds ??
          undefined,
        estimatedRemainingSecondsFromApi:
          statusQuery.data?.estimatedRemainingSeconds ?? undefined,
      }),
    [
      runQuery.data,
      statusQuery.data?.averageSucceededBackupDurationSeconds,
      statusQuery.data?.estimatedRemainingSeconds,
    ],
  );

  return {
    data: progress,
    run: runQuery.data,
    backupRunId: resolvedId,
    isLoading: Boolean(resolvedId) && runQuery.isLoading && !runQuery.data,
    isFetching: runQuery.isFetching || statusQuery.isFetching,
    isError: Boolean(resolvedId) && runQuery.isError,
    refetch: runQuery.refetch,
  };
}
