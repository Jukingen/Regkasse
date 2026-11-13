'use client';

import { Table } from 'antd';
import React from 'react';

import { useI18n } from '@/i18n';

export type CustomerAddressRow = {
  id: string;
  customerName: string;
  street: string;
  postalCode: string;
  city: string;
  contact: string;
};

export function CustomerAddressesTable({
  rows,
  loading,
}: {
  rows: CustomerAddressRow[];
  loading?: boolean;
}) {
  const { t } = useI18n();
  return (
    <Table<CustomerAddressRow>
      rowKey="id"
      loading={loading}
      dataSource={rows}
      pagination={false}
      locale={{ emptyText: t('admin.customerAddresses.empty') }}
      columns={[
        { title: t('admin.customerAddresses.colCustomer'), dataIndex: 'customerName' },
        { title: t('admin.customerAddresses.colStreet'), dataIndex: 'street' },
        { title: t('admin.customerAddresses.colPostal'), dataIndex: 'postalCode' },
        { title: t('admin.customerAddresses.colCity'), dataIndex: 'city' },
        { title: t('admin.customerAddresses.colContact'), dataIndex: 'contact' },
      ]}
    />
  );
}
