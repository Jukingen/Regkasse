"use client";

import { useCallback } from "react";
import type { BackupRunResponseDto } from "@/api/generated/model";
import {
  BACKUP_ACTIVE_POLL_MS,
  computeRunDetailRefetchIntervalMs,
  isBackupLatestRunActiveStatus,
} from "@/features/backup-dr/logic/backupRunDetailPollPolicy";

/** Aligns backup-related list queries with idle vs active-backup pacing (matches legacy dashboard intervals). */
export const BACKUP_DASHBOARD_IDLE_POLL_MS = 60_000;

/** Default page sizing for orchestration-loading + backup history UX. */
export const BACKUP_RECENT_RUNS_PAGE_SIZE = 15;
/** Metrik / grafik penceresi için daha geniş örneklem (sunucu sayfalı). */
export const BACKUP_METRICS_RUNS_PAGE_SIZE = 50;
export const BACKUP_RESTORE_HISTORY_PAGE_SIZE = 10;

export function usePollBackupLatestDashboardInterval(): (
  q: { state: { data?: unknown } },
) => number {
  return useCallback((q: { state: { data?: unknown } }) => {
    const data = q.state.data as
      | { latestRun?: { status?: number } }
      | undefined;
    const s = data?.latestRun?.status;
    if (isBackupLatestRunActiveStatus(s)) return BACKUP_ACTIVE_POLL_MS;
    return BACKUP_DASHBOARD_IDLE_POLL_MS;
  }, []);
}

/** Keeps auxiliary backup queries pacing with current latest-run terminal/active state (legacy dashboard parity). */
export function usePollAlignedWithLatestDashboardBackup(
  latestStatus: number | undefined,
): (query: unknown) => number {
  return useCallback(
    (_query: unknown) =>
      isBackupLatestRunActiveStatus(latestStatus)
        ? BACKUP_ACTIVE_POLL_MS
        : BACKUP_DASHBOARD_IDLE_POLL_MS,
    [latestStatus],
  );
}

/** Run-by-id pacing used by BackupStatusCard and dashboard ornaments (terminal catch-up semantics preserved). */
export function usePollRunDetailDashboardInterval(
  latestRunId: string | undefined,
  latestStatus: number | undefined,
): (query: { state: { data?: BackupRunResponseDto | undefined } }) =>
  | number
  | false {
  return useCallback(
    (query: { state: { data?: BackupRunResponseDto | undefined } }) =>
      computeRunDetailRefetchIntervalMs({
        latestRunId,
        latestStatus,
        detail: query.state.data,
      }),
    [latestRunId, latestStatus],
  );
}

/** Restore-drill queues: short poll while queued/running, slower once settled. */
export function usePollRestoreVerificationDashboardInterval(): (
  q: { state: { data?: unknown } },
) => number {
  return useCallback((q: { state: { data?: unknown } }) => {
    const row = q.state.data as { status?: number } | null | undefined;
    const s = row?.status;
    if (s === 0 || s === 1) return 8_000;
    return 15_000;
  }, []);
}
