'use client';

import { Table } from 'antd';
import React from 'react';

import { useI18n } from '@/i18n';

export type ImeiRow = {
  id: string;
  imei: string;
  productName: string;
  status: string;
  soldAt: string;
  warrantyMonths: number;
};

export function ImeiTable({ rows, loading }: { rows: ImeiRow[]; loading?: boolean }) {
  const { t } = useI18n();
  return (
    <Table<ImeiRow>
      rowKey="id"
      loading={loading}
      dataSource={rows}
      pagination={false}
      locale={{ emptyText: t('admin.imeis.empty') }}
      columns={[
        { title: t('admin.imeis.colImei'), dataIndex: 'imei' },
        { title: t('admin.imeis.colProduct'), dataIndex: 'productName' },
        { title: t('admin.imeis.colStatus'), dataIndex: 'status' },
        { title: t('admin.imeis.colSoldAt'), dataIndex: 'soldAt' },
        { title: t('admin.imeis.colWarranty'), dataIndex: 'warrantyMonths' },
      ]}
    />
  );
}
