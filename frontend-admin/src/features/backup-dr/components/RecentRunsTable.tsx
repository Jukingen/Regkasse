'use client';

import { Alert, Button, Card, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { TablePaginationConfig } from 'antd/es/table/interface';
import React from 'react';

import type { BackupRunResponseDto } from '@/api/generated/model';

export interface RecentRunsTableProps {
  title: string;
  rowKey: string;
  dataSource: BackupRunResponseDto[];
  columns: ColumnsType<BackupRunResponseDto>;
  loading: boolean;
  queryError: boolean;
  t: (k: string) => string;
  onRetry?: () => void;
  pagination?: false | TablePaginationConfig;
  onRowClick?: (row: BackupRunResponseDto) => void;
}

export function RecentRunsTable({
  title,
  rowKey,
  dataSource,
  columns,
  loading,
  queryError,
  t,
  onRetry,
  pagination = false,
  onRowClick,
}: RecentRunsTableProps) {
  return (
    <Card title={title} size="small">
      {queryError && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 12 }}
          title={t('backupDr.errors.partialTable')}
          action={
            onRetry ? (
              <Button type="link" size="small" onClick={onRetry}>
                {t('backupDr.actions.refresh')}
              </Button>
            ) : undefined
          }
        />
      )}
      <Table<BackupRunResponseDto>
        rowKey={rowKey}
        size="small"
        loading={loading}
        dataSource={dataSource}
        columns={columns}
        pagination={pagination}
        onRow={
          onRowClick
            ? (record) => ({
                onClick: () => onRowClick(record),
                style: { cursor: 'pointer' },
              })
            : undefined
        }
      />
      <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
        {t('backupDr.runs.statusHint')}
      </Typography.Paragraph>
    </Card>
  );
}
