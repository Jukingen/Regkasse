'use client';

// Read-only back-office table list; API is still in-memory demo (TableController) until persisted tables exist.

import React from 'react';
import { Alert, Card, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n/I18nProvider';
import { useGetApiTable } from '@/api/generated/table/table';
import type { TableInfo } from '@/api/generated/model';

export default function TablesAdminPage() {
  const { t } = useI18n();
  const { data, isLoading, isError, error } = useGetApiTable();

  const columns: ColumnsType<TableInfo> = [
    { title: t('adminShell.tablesAdmin.colId'), dataIndex: 'id', width: 80 },
    { title: t('adminShell.tablesAdmin.colNumber'), dataIndex: 'number', width: 90 },
    {
      title: t('adminShell.tablesAdmin.colStatus'),
      dataIndex: 'status',
      render: (s: string) => <Tag color={s === 'Available' ? 'green' : 'blue'}>{s}</Tag>,
    },
    { title: t('adminShell.tablesAdmin.colCapacity'), dataIndex: 'capacity', width: 100 },
  ];

  const errMsg = error instanceof Error ? error.message : String(error ?? '');

  return (
    <div style={{ paddingBottom: 24 }}>
      <AdminPageHeader
        title={t('nav.tables')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('nav.tables') }]}
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('adminShell.tablesAdmin.intro')}
        </Typography.Paragraph>
      </AdminPageHeader>

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message={t('adminShell.tablesAdmin.dataHintTitle')}
        description={t('adminShell.tablesAdmin.dataHintBody')}
      />

      {isError ? (
        <Alert type="error" showIcon message={t('adminShell.tablesAdmin.loadError')} description={errMsg} />
      ) : (
        <Card bordered={false}>
          <Table<TableInfo>
            rowKey="id"
            loading={isLoading}
            columns={columns}
            dataSource={data ?? []}
            pagination={false}
          />
        </Card>
      )}
    </div>
  );
}
