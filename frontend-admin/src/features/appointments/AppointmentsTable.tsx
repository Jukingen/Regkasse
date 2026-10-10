'use client';

import { Table } from 'antd';
import React from 'react';

import { useI18n } from '@/i18n';

export type AppointmentRow = {
  id: string;
  startUtc: string;
  endUtc: string;
  staffId: string;
  status: string;
  notes: string;
};

export function AppointmentsTable({
  rows,
  loading,
}: {
  rows: AppointmentRow[];
  loading?: boolean;
}) {
  const { t } = useI18n();
  return (
    <Table<AppointmentRow>
      rowKey="id"
      loading={loading}
      dataSource={rows}
      pagination={false}
      locale={{ emptyText: t('admin.appointments.empty') }}
      columns={[
        { title: t('admin.appointments.colStart'), dataIndex: 'startUtc' },
        { title: t('admin.appointments.colEnd'), dataIndex: 'endUtc' },
        { title: t('admin.appointments.colStaff'), dataIndex: 'staffId' },
        { title: t('admin.appointments.colStatus'), dataIndex: 'status' },
        { title: t('admin.appointments.colNotes'), dataIndex: 'notes' },
      ]}
    />
  );
}
