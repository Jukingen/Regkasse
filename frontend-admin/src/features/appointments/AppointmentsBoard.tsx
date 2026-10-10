'use client';

import { Segmented, Select, Space } from 'antd';
import React, { useMemo } from 'react';

import { AppointmentsTable, type AppointmentRow } from '@/features/appointments/AppointmentsTable';
import { useI18n } from '@/i18n';

export type AppointmentView = 'day' | 'week' | 'table';

const STATUS_VALUES = ['Booked', 'Confirmed', 'Completed', 'Cancelled', 'NoShow'] as const;

function dayKey(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  return date.toISOString().slice(0, 10);
}

export function AppointmentsBoard({
  rows,
  loading,
  view,
  onViewChange,
  staffId,
  onStaffChange,
  status,
  onStatusChange,
  staffOptions,
}: {
  rows: AppointmentRow[];
  loading?: boolean;
  view: AppointmentView;
  onViewChange: (view: AppointmentView) => void;
  staffId?: string;
  onStaffChange: (staffId: string | undefined) => void;
  status?: string;
  onStatusChange: (status: string | undefined) => void;
  staffOptions: string[];
}) {
  const { t } = useI18n();
  const grouped = useMemo(() => {
    const map = new Map<string, AppointmentRow[]>();
    for (const row of rows) {
      const key = dayKey(row.startUtc);
      const list = map.get(key) ?? [];
      list.push(row);
      map.set(key, list);
    }
    return [...map.entries()];
  }, [rows]);

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <Space wrap>
        <Segmented
          value={view}
          onChange={(value) => onViewChange(value as AppointmentView)}
          options={[
            { label: t('admin.appointments.viewDay'), value: 'day' },
            { label: t('admin.appointments.viewWeek'), value: 'week' },
            { label: t('admin.appointments.viewTable'), value: 'table' },
          ]}
        />
        <Select
          allowClear
          style={{ minWidth: 180 }}
          placeholder={t('admin.appointments.staff')}
          value={staffId}
          onChange={(value) => onStaffChange(value)}
          options={staffOptions.map((id) => ({ value: id, label: id }))}
        />
        <Select
          allowClear
          style={{ minWidth: 180 }}
          placeholder={t('admin.appointments.status')}
          value={status}
          onChange={(value) => onStatusChange(value)}
          options={STATUS_VALUES.map((value) => ({
            value,
            label: t(`admin.appointments.status${value}`),
          }))}
        />
      </Space>
      {view === 'table' ? <AppointmentsTable rows={rows} loading={loading} /> : null}
      {view !== 'table' ? (
        <div style={{ display: 'grid', gap: 12, gridTemplateColumns: view === 'week' ? 'repeat(7, minmax(0, 1fr))' : '1fr' }}>
          {grouped.map(([day, dayRows]) => (
            <section key={day}>
              <strong>{day}</strong>
              {dayRows.map((row) => (
                <div key={row.id}>
                  {row.staffId} · {row.status}
                </div>
              ))}
            </section>
          ))}
        </div>
      ) : null}
    </Space>
  );
}
