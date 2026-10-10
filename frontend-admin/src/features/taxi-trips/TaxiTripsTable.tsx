'use client';

import { Table } from 'antd';
import React from 'react';

import { useI18n } from '@/i18n';

export type TaxiTripRow = {
  id: string;
  occurredAt: string;
  customerName: string;
  routeFrom: string;
  routeTo: string;
  routeKm: string;
  amount: string;
  cashRegister: string;
};

export function TaxiTripsTable({ rows, loading }: { rows: TaxiTripRow[]; loading?: boolean }) {
  const { t } = useI18n();
  return (
    <Table<TaxiTripRow>
      rowKey="id"
      loading={loading}
      dataSource={rows}
      pagination={false}
      locale={{ emptyText: t('admin.taxiTrips.empty') }}
      columns={[
        { title: t('admin.taxiTrips.colDate'), dataIndex: 'occurredAt' },
        { title: t('admin.taxiTrips.colCustomer'), dataIndex: 'customerName' },
        { title: t('admin.taxiTrips.colFrom'), dataIndex: 'routeFrom' },
        { title: t('admin.taxiTrips.colTo'), dataIndex: 'routeTo' },
        { title: t('admin.taxiTrips.colKm'), dataIndex: 'routeKm' },
        { title: t('admin.taxiTrips.colAmount'), dataIndex: 'amount' },
        { title: t('admin.taxiTrips.colRegister'), dataIndex: 'cashRegister' },
      ]}
    />
  );
}
