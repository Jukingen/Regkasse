'use client';

import { DatePicker } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import React, { useMemo, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { useAdminTaxiTrips } from '@/features/taxi-trips/api';
import { TaxiTripsTable, type TaxiTripRow } from '@/features/taxi-trips/TaxiTripsTable';
import { useI18n } from '@/i18n';

export default function AdminTaxiTripsPage() {
  const { t } = useI18n();
  const [range, setRange] = useState<[Dayjs, Dayjs]>([dayjs().subtract(7, 'day'), dayjs()]);
  const query = useAdminTaxiTrips({
    from: range[0].startOf('day').toISOString(),
    to: range[1].endOf('day').toISOString(),
  });
  const rows = useMemo<TaxiTripRow[]>(
    () =>
      (query.data ?? []).map((row) => ({
        id: row.paymentId,
        occurredAt: row.occurredAtUtc,
        customerName: row.customerName?.trim() || '—',
        routeFrom: row.routeFrom?.trim() || '—',
        routeTo: row.routeTo?.trim() || '—',
        routeKm: row.routeKm == null ? '—' : String(row.routeKm),
        amount: String(row.amount),
        cashRegister: row.cashRegisterNumber?.trim() || '—',
      })),
    [query.data]
  );

  return (
    <AdminPageShell>
      <AdminPageHeader title={t('admin.taxiTrips.title')} subtitle={t('admin.taxiTrips.subtitle')} />
      <DatePicker.RangePicker
        value={range}
        onChange={(value) => {
          if (value?.[0] && value[1]) setRange([value[0], value[1]]);
        }}
      />
      <TaxiTripsTable rows={rows} loading={query.isLoading} />
    </AdminPageShell>
  );
}
