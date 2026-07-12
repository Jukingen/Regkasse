'use client';

import React, { useState } from 'react';
import { Card, Select, DatePicker, Space, Button } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { ADMIN_NAV_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { CashRegisterSelector } from '@/components/CashRegisterSelector';
import { CardTransactionsTable } from '@/features/payments/components/CardTransactionsTable';
import { useCardTransactions } from '@/features/payments/hooks/useCardTransactions';
import { useI18n } from '@/i18n';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';

export function CardTransactionsPage() {
  const { t } = useI18n();
  const ts = (key: string, fallback?: string) => {
    const fullKey = `cardTransactions:${key}`;
    const value = t(fullKey);
    return value === fullKey ? (fallback ?? key) : value;
  };

  const [status, setStatus] = useState<string | undefined>();
  const [registerId, setRegisterId] = useState<string | undefined>();
  const [dateRange, setDateRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);

  const {
    data,
    isLoading,
    isFetching,
    totalCount,
    pageNumber,
    pageSize,
    setPageNumber,
    setPageSize,
    setFilters,
    refetch,
  } = useCardTransactions();

  const applyFilters = (patch: {
    status?: string;
    registerId?: string;
    dateRange?: [Dayjs | null, Dayjs | null] | null;
  }) => {
    const nextStatus = patch.status !== undefined ? patch.status : status;
    const nextRegisterId = patch.registerId !== undefined ? patch.registerId : registerId;
    const nextRange = patch.dateRange !== undefined ? patch.dateRange : dateRange;

    if (patch.status !== undefined) setStatus(patch.status);
    if (patch.registerId !== undefined) setRegisterId(patch.registerId);
    if (patch.dateRange !== undefined) setDateRange(patch.dateRange);

    setFilters({
      status: nextStatus,
      cashRegisterId: nextRegisterId,
      fromUtc: nextRange?.[0]?.startOf('day').toISOString(),
      toUtc: nextRange?.[1]?.endOf('day').toISOString(),
    });
  };

  return (
    <>
      <AdminPageHeader
        title={ts('title')}
        breadcrumbs={[ADMIN_OVERVIEW_CRUMB, { title: ADMIN_NAV_LABELS.payments }, { title: ts('title') }]}
      />
      <AdminPageShell>
        <Card>
          <Space wrap style={{ marginBottom: 16 }}>
            <DatePicker.RangePicker format={DAYJS_DATE_FORMAT}
              value={dateRange}
              onChange={(v) => applyFilters({ dateRange: v })}
              allowEmpty={[true, true]}
            />
            <Select
              allowClear
              placeholder={ts('filters.status')}
              style={{ minWidth: 160 }}
              value={status}
              onChange={(v) => applyFilters({ status: v })}
              options={['Created', 'Pending', 'Succeeded', 'Failed', 'Cancelled', 'Refunded'].map((s) => ({
                value: s,
                label: ts(`status.${s}`, s),
              }))}
            />
            <CashRegisterSelector
              value={registerId}
              onChange={(v) => applyFilters({ registerId: v })}
              showFormItem={false}
              required={false}
              allowClear
              placeholder={ts('filters.register')}
              style={{ minWidth: 200 }}
            />
            <Button icon={<ReloadOutlined />} onClick={refetch} loading={isFetching}>
              {ts('actions.refresh')}
            </Button>
          </Space>

          <CardTransactionsTable transactions={data} loading={isLoading} emptyText={ts('empty')} pagination={{
              current: pageNumber,
              pageSize,
              total: totalCount,
              showSizeChanger: true,
              onChange: (p, ps) => {
                setPageNumber(p);
                setPageSize(ps);
              },
            }}
          />
        </Card>
      </AdminPageShell>
    </>
  );
}
