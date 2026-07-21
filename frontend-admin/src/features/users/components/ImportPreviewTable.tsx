'use client';

import { Table, Typography } from 'antd';
import React from 'react';

import type { BulkImportPreviewRow } from '@/features/users/api/bulkImport';
import { useI18n } from '@/i18n';

type Props = {
  rows: BulkImportPreviewRow[];
  totalRows: number;
  loading?: boolean;
};

/** First N rows of a bulk import file before starting the job. */
export function ImportPreviewTable({ rows, totalRows, loading }: Props) {
  const { t } = useI18n();

  return (
    <>
      <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
        {t('users.bulkImport.preview', { shown: rows.length, total: totalRows })}
      </Typography.Text>
      <Table
        size="small"
        loading={loading}
        pagination={false}
        rowKey={(r) => String(r.row)}
        dataSource={rows}
        scroll={{ x: 640 }}
        columns={[
          { title: t('users.bulkImport.columns.row'), dataIndex: 'row', width: 64 },
          { title: t('users.bulkImport.columns.email'), dataIndex: 'email', ellipsis: true },
          { title: t('users.bulkImport.columns.username'), dataIndex: 'username', ellipsis: true },
          { title: t('users.bulkImport.columns.firstName'), dataIndex: 'firstName', width: 100 },
          { title: t('users.bulkImport.columns.lastName'), dataIndex: 'lastName', width: 100 },
          { title: t('users.bulkImport.columns.role'), dataIndex: 'role', width: 100 },
          { title: t('users.bulkImport.columns.tenant'), dataIndex: 'tenantSlug', width: 100 },
        ]}
      />
    </>
  );
}
