'use client';

import React from 'react';
import { Alert, Button, Card, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { RestoreVerificationRunResponseDto } from '@/api/generated/model';

export interface RecentRestoreDrillsTableProps {
  title: string;
  rowKey: string;
  dataSource: RestoreVerificationRunResponseDto[];
  columns: ColumnsType<RestoreVerificationRunResponseDto>;
  loading: boolean;
  queryError: boolean;
  emptyText: string;
  t: (k: string) => string;
  onRetry?: () => void;
}

export function RecentRestoreDrillsTable({
  title,
  rowKey,
  dataSource,
  columns,
  loading,
  queryError,
  emptyText,
  t,
  onRetry,
}: RecentRestoreDrillsTableProps) {
  return (
    <Card title={title} size="small">
      {queryError && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 12 }}
          message={t('backupDr.errors.partialTableRestore')}
          action={
            onRetry ? (
              <Button type="link" size="small" onClick={onRetry}>
                {t('backupDr.actions.refresh')}
              </Button>
            ) : undefined
          }
        />
      )}
      <Table<RestoreVerificationRunResponseDto>
        rowKey={rowKey}
        size="small"
        loading={loading}
        dataSource={dataSource}
        columns={columns}
        pagination={false}
        locale={{ emptyText }}
      />
      <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
        {t('backupDr.restoreHistory.statusHint')}
      </Typography.Paragraph>
    </Card>
  );
}
