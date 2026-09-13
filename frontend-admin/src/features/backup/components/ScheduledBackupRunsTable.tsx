'use client';

import { Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import React, { useMemo } from 'react';

import type { BackupRunResponseDto } from '@/api/generated/model';
import { BackupStatusBadge } from '@/features/backup/components/BackupStatusBadge';
import { useBackupRuns } from '@/features/backup/api/backupHooks';
import { backupRunTrafficLightRowClass } from '@/features/backup/logic/backupRunTrafficLight';
import trafficLight from '@/features/backup/backupRunTrafficLight.module.css';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/lib/dateUtils';

const PAGE_SIZE = 5;

export function ScheduledBackupRunsTable() {
  const { t } = useI18n();
  const query = useBackupRuns({ page: 1, pageSize: PAGE_SIZE, createdBy: 'system' });

  const columns: ColumnsType<BackupRunResponseDto> = useMemo(
    () => [
      {
        title: t('backupDr.scheduleSettings.recentScheduledWhen'),
        dataIndex: 'requestedAt',
        render: (iso: string | undefined) => formatDateTime(iso),
      },
      {
        title: t('backupDr.scheduleSettings.recentScheduledStatus'),
        dataIndex: 'status',
        width: 160,
        render: (status: number | undefined) => <BackupStatusBadge status={status} />,
      },
      {
        title: t('backupDr.scheduleSettings.failureReason'),
        key: 'failure',
        ellipsis: true,
        render: (_: unknown, row) =>
          row.failureCode || row.failureDetail
            ? [row.failureCode, row.failureDetail].filter(Boolean).join(' — ')
            : '—',
      },
    ],
    [t]
  );

  return (
    <div>
      <Typography.Text strong>{t('backupDr.scheduleSettings.recentScheduledTitle')}</Typography.Text>
      <Table<BackupRunResponseDto>
        rowKey={(r) => r.id ?? r.requestedAt ?? ''}
        size="small"
        style={{ marginTop: 8 }}
        loading={query.isFetching}
        dataSource={query.data?.items ?? []}
        columns={columns}
        pagination={false}
        rowClassName={(row) => backupRunTrafficLightRowClass(row.status, trafficLight)}
        locale={{ emptyText: t('backupDr.scheduleSettings.noRunsYet') }}
      />
    </div>
  );
}
