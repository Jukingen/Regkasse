"use client";

import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { useI18n } from "@/i18n";
import {
  BACKUP_DASHBOARD_STATS_POLL_MS,
  getBackupDashboardStats,
  getBackupDashboardStatsQueryKey,
} from "@/features/backup/logic/backupDashboardStatsApi";
import { mapDashboardStatsToPerformance } from "@/features/backup/logic/backupPerformancePresentation";

/** Performance metrics from GET /api/admin/backup/dashboard/stats. */
export function useBackupPerformance(options?: { enabled?: boolean }) {
  const { t, formatLocale } = useI18n();
  const enabled = options?.enabled !== false;

  const statsQuery = useQuery({
    queryKey: getBackupDashboardStatsQueryKey(),
    queryFn: getBackupDashboardStats,
    enabled,
    refetchInterval: BACKUP_DASHBOARD_STATS_POLL_MS,
    refetchOnWindowFocus: true,
  });

  const data = useMemo(
    () => mapDashboardStatsToPerformance(statsQuery.data, formatLocale, t),
    [formatLocale, statsQuery.data, t],
  );

  return {
    data,
    isLoading: statsQuery.isLoading && !statsQuery.data,
    isFetching: statsQuery.isFetching,
    isError: statsQuery.isError,
    refetch: statsQuery.refetch,
  };
}
