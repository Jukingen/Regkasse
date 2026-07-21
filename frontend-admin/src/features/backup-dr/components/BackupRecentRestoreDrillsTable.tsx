'use client';

import { Tag, Tooltip } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import React, { useCallback, useMemo, useState } from 'react';

import { useGetApiAdminRestoreVerificationRuns } from '@/api/generated/admin-restore-verification/admin-restore-verification';
import {
  type RestoreVerificationRunResponseDto,
  RestoreVerificationRunResponseDtoStatus,
} from '@/api/generated/model';
import { RecentRestoreDrillsTable } from '@/features/backup-dr/components/RecentRestoreDrillsTable';
import {
  BACKUP_RESTORE_HISTORY_PAGE_SIZE,
  usePollRestoreVerificationDashboardInterval,
} from '@/features/backup-dr/logic/backupDashboardQueryTiming';
import {
  mapDumpInspectionTriState,
  mapRestoreVerificationStatusAntdColor,
} from '@/features/backup-dr/logic/backupDrMappers';
import {
  PG_RESTORE_LIST_FAILED,
  interpretPgRestoreListFailure,
  pgRestoreListFailureKindToStatusLabelKey,
  pgRestoreListFailureKindToTagColor,
} from '@/features/backup-dr/logic/restoreVerificationFailurePresentation';

export interface BackupRecentRestoreDrillsTableProps {
  formatDt: (iso: string | undefined | null, locale: string) => string;
  formatLocale: string;
  restoreStatusLabel: (status: number | undefined, t: (k: string) => string) => string;
  isSimulatedAdapterEnvironment: boolean;
  t: (k: string) => string;
  onRetryInvalidate: () => Promise<void>;
}

/** Restore drill history with server-backed pagination (`GET /api/admin/restore-verification/runs`). */
export function BackupRecentRestoreDrillsTable({
  formatDt,
  formatLocale,
  restoreStatusLabel,
  isSimulatedAdapterEnvironment,
  t,
  onRetryInvalidate,
}: BackupRecentRestoreDrillsTableProps) {
  const [page, setPage] = useState(1);
  const pollRestore = usePollRestoreVerificationDashboardInterval();

  const restoreHistoryQuery = useGetApiAdminRestoreVerificationRuns(
    { page, pageSize: BACKUP_RESTORE_HISTORY_PAGE_SIZE },
    {
      query: {
        refetchInterval: pollRestore,
        refetchOnWindowFocus: true,
      },
    }
  );

  const restoreHistoryColumns: ColumnsType<RestoreVerificationRunResponseDto> = useMemo(
    () => [
      {
        title: t('backupDr.latestRun.requested'),
        dataIndex: 'requestedAt',
        key: 'requestedAt',
        render: (x: string) => formatDt(x, formatLocale),
      },
      {
        title: t('backupDr.table.status'),
        dataIndex: 'status',
        key: 'status',
        render: (s: number | undefined, row: RestoreVerificationRunResponseDto) => {
          const listInterp =
            s === RestoreVerificationRunResponseDtoStatus.NUMBER_3 &&
            row.failureCode === PG_RESTORE_LIST_FAILED
              ? interpretPgRestoreListFailure({
                  run: row,
                  isSimulatedPipelineHeuristic: isSimulatedAdapterEnvironment,
                })
              : null;
          const color =
            listInterp != null
              ? pgRestoreListFailureKindToTagColor(listInterp.kind)
              : mapRestoreVerificationStatusAntdColor(s);
          const label =
            listInterp != null
              ? t(pgRestoreListFailureKindToStatusLabelKey(listInterp.kind))
              : restoreStatusLabel(s, t);
          return <Tag color={color}>{label}</Tag>;
        },
      },
      {
        title: t('backupDr.table.dumpInspection'),
        key: 'dump',
        render: (_: unknown, row: RestoreVerificationRunResponseDto) => {
          const p = mapDumpInspectionTriState(row);
          if (p === undefined) return '—';
          if (p) return t('backupDr.triState.ok');
          const listInterp =
            row.failureCode === PG_RESTORE_LIST_FAILED
              ? interpretPgRestoreListFailure({
                  run: row,
                  isSimulatedPipelineHeuristic: isSimulatedAdapterEnvironment,
                })
              : null;
          if (listInterp?.kind === 'fake_stub_expected')
            return t('backupDr.triState.dumpInspectionNotApplicableStub');
          return t('backupDr.triState.fail');
        },
      },
      {
        title: t('backupDr.table.restoreAttempt'),
        key: 'attempt',
        render: (_: unknown, row) => {
          if (!row.restoreAttemptExecuted) return t('backupDr.restoreAttempt.notRun');
          if (row.restoreAttemptPassed === true) return t('backupDr.triState.ok');
          if (row.restoreAttemptPassed === false) return t('backupDr.triState.fail');
          return '—';
        },
      },
      {
        title: t('backupDr.table.failure'),
        dataIndex: 'failureCode',
        key: 'failureCode',
        render: (c: string | null | undefined, row: RestoreVerificationRunResponseDto) => {
          const code = c ?? '—';
          if (row.failureCode === PG_RESTORE_LIST_FAILED) {
            const listInterp = interpretPgRestoreListFailure({
              run: row,
              isSimulatedPipelineHeuristic: isSimulatedAdapterEnvironment,
            });
            if (listInterp?.kind === 'fake_stub_expected') {
              return (
                <Tooltip
                  title={t('backupDr.restoreVerification.fakePipeline.pgRestoreListTooltip')}
                >
                  <span>{code}</span>
                </Tooltip>
              );
            }
            if (listInterp) {
              const tk = `backupDr.restoreVerification.realPipeline.failureTooltips.${listInterp.kind}`;
              const title = t(tk);
              return title !== tk ? (
                <Tooltip title={title}>
                  <span>{code}</span>
                </Tooltip>
              ) : (
                code
              );
            }
          }
          return code;
        },
      },
    ],
    [formatDt, formatLocale, isSimulatedAdapterEnvironment, restoreStatusLabel, t]
  );

  const totalCount = restoreHistoryQuery.data?.totalCount ?? 0;

  const onRetry = useCallback(() => void onRetryInvalidate(), [onRetryInvalidate]);

  return (
    <RecentRestoreDrillsTable
      title={t('backupDr.restoreHistory.title')}
      rowKey="id"
      dataSource={restoreHistoryQuery.data?.items ?? []}
      columns={restoreHistoryColumns}
      loading={restoreHistoryQuery.isFetching}
      queryError={restoreHistoryQuery.isError}
      emptyText={t('backupDr.restoreHistory.empty')}
      t={t}
      onRetry={onRetry}
      pagination={{
        current: page,
        pageSize: BACKUP_RESTORE_HISTORY_PAGE_SIZE,
        total: totalCount,
        showSizeChanger: false,
        onChange: (p) => setPage(p),
      }}
    />
  );
}
