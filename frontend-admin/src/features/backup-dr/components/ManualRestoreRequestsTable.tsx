'use client';

import React, { useMemo, useState } from 'react';
import { Button, Card, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useQuery } from '@tanstack/react-query';
import {
  MANUAL_RESTORE_HISTORY_PATH,
  getManualRestoreHistory,
  type RestoreRequestStatusDto,
} from '@/features/backup-dr/logic/manualRestoreApi';
import { manualRestoreStatusTagColor } from '@/features/backup-dr/logic/manualRestorePresentation';

export interface ManualRestoreRequestsTableProps {
  canApprove: boolean;
  onApprove: (requestId: string) => void;
  formatDt: (iso: string | undefined | null, locale: string) => string;
  formatLocale: string;
  t: (key: string, options?: Record<string, string | number>) => string;
}

export function ManualRestoreRequestsTable({
  canApprove,
  onApprove,
  formatDt,
  formatLocale,
  t,
}: ManualRestoreRequestsTableProps) {
  const [page, setPage] = useState(1);
  const pageSize = 10;

  const historyQuery = useQuery({
    queryKey: [MANUAL_RESTORE_HISTORY_PATH, page, pageSize],
    queryFn: () => getManualRestoreHistory(page, pageSize),
    enabled: canApprove,
  });

  const columns: ColumnsType<RestoreRequestStatusDto> = useMemo(
    () => [
      {
        title: t('backupDr.manualRestore.history.requestedAt'),
        dataIndex: 'requestedAt',
        render: (v: string) => formatDt(v, formatLocale),
      },
      {
        title: t('backupDr.manualRestore.fields.status'),
        dataIndex: 'status',
        render: (s: string) => {
          const normalized =
            s === 'PendingApproval' ? 'pendingApproval' : s.charAt(0).toLowerCase() + s.slice(1);
          const key = `backupDr.manualRestore.status.${normalized}`;
          const label = t(key) === key ? s : t(key);
          return <Tag color={manualRestoreStatusTagColor(s)}>{label}</Tag>;
        },
      },
      {
        title: t('backupDr.manualRestore.approvalModal.requestedBy'),
        dataIndex: 'requestedByEmail',
        ellipsis: true,
      },
      {
        title: t('backupDr.manualRestore.fields.targetDatabase'),
        dataIndex: 'targetDatabaseName',
        ellipsis: true,
      },
      {
        title: t('backupDr.manualRestore.table.actions'),
        key: 'actions',
        render: (_: unknown, row) =>
          row.status === 'PendingApproval' ? (
            <Button size="small" type="primary" onClick={() => onApprove(row.requestId)}>
              {t('backupDr.manualRestore.approvalModal.openApprove')}
            </Button>
          ) : (
            '—'
          ),
      },
    ],
    [formatDt, formatLocale, onApprove, t],
  );

  if (!canApprove) return null;

  return (
    <Card size="small" title={t('backupDr.manualRestore.history.title')}>
      <Table<RestoreRequestStatusDto>
        size="small"
        rowKey="requestId"
        loading={historyQuery.isLoading}
        dataSource={historyQuery.data?.items ?? []}
        columns={columns}
        pagination={{
          current: page,
          pageSize,
          total: historyQuery.data?.totalCount ?? 0,
          onChange: setPage,
          showSizeChanger: false,
        }}
      />
    </Card>
  );
}
