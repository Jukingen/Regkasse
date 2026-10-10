'use client';

import { DatePicker, Space } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import React, { useMemo, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { useAdminAppointments } from '@/features/appointments/api';
import {
  AppointmentsBoard,
  type AppointmentView,
} from '@/features/appointments/AppointmentsBoard';
import type { AppointmentRow } from '@/features/appointments/AppointmentsTable';
import { useI18n } from '@/i18n';

function rangeFor(view: AppointmentView, anchor: Dayjs): { from: string; to: string } {
  if (view === 'day') {
    return { from: anchor.startOf('day').toISOString(), to: anchor.endOf('day').toISOString() };
  }
  const start = anchor.startOf('week');
  return { from: start.toISOString(), to: start.add(6, 'day').endOf('day').toISOString() };
}

export default function AdminAppointmentsPage() {
  const { t } = useI18n();
  const [view, setView] = useState<AppointmentView>('table');
  const [anchor, setAnchor] = useState(() => dayjs());
  const [staffId, setStaffId] = useState<string | undefined>();
  const [status, setStatus] = useState<string | undefined>();
  const range = rangeFor(view === 'day' ? 'day' : 'week', anchor);
  const query = useAdminAppointments({
    from: range.from,
    to: range.to,
    staffId,
    status,
  });
  const rows = useMemo<AppointmentRow[]>(
    () =>
      (query.data ?? []).map((row) => ({
        id: row.id,
        startUtc: row.startUtc,
        endUtc: row.endUtc,
        staffId: row.staffId?.trim() || '—',
        status: row.status,
        notes: row.notes?.trim() || '—',
      })),
    [query.data]
  );
  const staffOptions = useMemo(
    () => [...new Set(rows.map((row) => row.staffId).filter((id) => id !== '—'))],
    [rows]
  );

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('admin.appointments.title')}
        subtitle={t('admin.appointments.subtitle')}
      />
      <Space>
        <DatePicker value={anchor} onChange={(value) => value && setAnchor(value)} />
      </Space>
      <AppointmentsBoard
        rows={rows}
        loading={query.isLoading}
        view={view}
        onViewChange={setView}
        staffId={staffId}
        onStaffChange={setStaffId}
        status={status}
        onStatusChange={setStatus}
        staffOptions={staffOptions}
      />
    </AdminPageShell>
  );
}
