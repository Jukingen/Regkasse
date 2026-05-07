"use client";

import React, { useCallback, useMemo, useState } from "react";
import { Tag } from "antd";
import type { ColumnsType } from "antd/es/table";
import {
  useGetApiAdminBackupRuns,
  useGetApiAdminBackupStatusLatest,
} from "@/api/generated/admin-backup/admin-backup";
import type { BackupRunResponseDto } from "@/api/generated/model";
import { RecentRunsTable } from "@/features/backup-dr/components/RecentRunsTable";
import {
  BACKUP_RECENT_RUNS_PAGE_SIZE,
  usePollAlignedWithLatestDashboardBackup,
  usePollBackupLatestDashboardInterval,
} from "@/features/backup-dr/logic/backupDashboardQueryTiming";
import { mapBackupRunStatusAntdColor } from "@/features/backup-dr/logic/backupDrMappers";
import { apiNullableToUndefined } from "@/features/backup-dr/logic/backupDrDtoNormalize";

export interface BackupRecentRunsTableProps {
  backupStatusLabel: (status: number | undefined, t: (k: string) => string) => string;
  formatDt: (iso: string | undefined | null, locale: string) => string;
  formatLocale: string;
  t: (k: string) => string;
  onRetryInvalidate: () => Promise<void>;
}

/** Recent backup jobs with server-backed pagination (`GET /api/admin/backup/runs`). */
export function BackupRecentRunsTable({
  backupStatusLabel,
  formatDt,
  formatLocale,
  t,
  onRetryInvalidate,
}: BackupRecentRunsTableProps) {
  const [page, setPage] = useState(1);

  const pollPeek = usePollBackupLatestDashboardInterval();
  const alignSource = useGetApiAdminBackupStatusLatest({
    query: {
      refetchInterval: pollPeek,
      refetchOnWindowFocus: true,
    },
  });
  const latestPeek = apiNullableToUndefined(alignSource.data?.latestRun);
  const pollAlignedRuns = usePollAlignedWithLatestDashboardBackup(latestPeek?.status);

  const runsQuery = useGetApiAdminBackupRuns(
    { page, pageSize: BACKUP_RECENT_RUNS_PAGE_SIZE },
    {
      query: {
        refetchInterval: pollAlignedRuns,
        refetchOnWindowFocus: true,
      },
    },
  );

  const columns: ColumnsType<BackupRunResponseDto> = useMemo(
    () => [
      {
        title: t("backupDr.table.requestedAt"),
        dataIndex: "requestedAt",
        key: "requestedAt",
        render: (v: string) => formatDt(v, formatLocale),
      },
      {
        title: t("backupDr.table.status"),
        dataIndex: "status",
        key: "status",
        render: (s: number | undefined) => (
          <Tag color={mapBackupRunStatusAntdColor(s)}>{backupStatusLabel(s, t)}</Tag>
        ),
      },
      {
        title: t("backupDr.table.adapter"),
        dataIndex: "adapterKind",
        key: "adapterKind",
      },
      {
        title: t("backupDr.table.completedAt"),
        dataIndex: "completedAt",
        key: "completedAt",
        render: (v: string | null) => formatDt(v, formatLocale),
      },
      {
        title: t("backupDr.table.failure"),
        dataIndex: "failureCode",
        key: "failureCode",
        render: (c: string | null) => c ?? "—",
      },
    ],
    [backupStatusLabel, formatDt, formatLocale, t],
  );

  const totalCount = runsQuery.data?.totalCount ?? 0;

  const onRetry = useCallback(() => void onRetryInvalidate(), [onRetryInvalidate]);

  return (
    <RecentRunsTable
      title={t("backupDr.runs.title")}
      rowKey="id"
      dataSource={runsQuery.data?.items ?? []}
      columns={columns}
      loading={runsQuery.isFetching}
      queryError={runsQuery.isError}
      t={t}
      onRetry={onRetry}
      pagination={{
        current: page,
        pageSize: BACKUP_RECENT_RUNS_PAGE_SIZE,
        total: totalCount,
        showSizeChanger: false,
        onChange: (p) => setPage(p),
      }}
    />
  );
}
