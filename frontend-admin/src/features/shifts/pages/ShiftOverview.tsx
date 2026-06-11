'use client';

import React, { useCallback, useMemo, useState } from 'react';
import { Alert, Button, Card, DatePicker, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ReloadOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import Link from 'next/link';

import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import type { CashRegister } from '@/api/generated/model';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import {
  type AdminDailyClosingOverviewRow,
  type AdminShiftRow,
} from '@/features/shifts/api/shiftsOverview';
import { useAdminShiftOverview } from '@/features/shifts/hooks/useAdminShiftOverview';
import { FORMAT_EMPTY_DISPLAY, formatCurrency, formatDateTime, useI18n } from '@/i18n';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';

function normalizeRegisters(data: unknown): CashRegister[] {
  if (Array.isArray(data)) return data as CashRegister[];
  const r = (data as { registers?: CashRegister[] } | undefined)?.registers;
  return Array.isArray(r) ? r : [];
}

function statusTagColor(status: string): string {
  switch (status) {
    case 'Active':
      return 'green';
    case 'Discrepancy':
      return 'orange';
    case 'Completed':
    default:
      return 'blue';
  }
}

export const ShiftOverview: React.FC = () => {
  const { t, formatLocale } = useI18n();
  const ts = useCallback((path: string) => t(`shifts:${path}`), [t]);

  const [registerId, setRegisterId] = useState<string | undefined>();
  const [fromDay, setFromDay] = useState<Dayjs | null>(null);
  const [toDay, setToDay] = useState<Dayjs | null>(null);

  const registersQ = useGetApiCashRegister();
  const registerRows = useMemo(() => normalizeRegisters(registersQ.data as unknown), [registersQ.data]);

  const queryParams = useMemo(
    () => ({
      cashRegisterId: registerId,
      fromUtc: fromDay ? fromDay.startOf('day').toISOString() : undefined,
      toUtc: toDay ? toDay.add(1, 'day').startOf('day').toISOString() : undefined,
      limit: 200,
    }),
    [fromDay, registerId, toDay],
  );

  const overviewQ = useAdminShiftOverview(queryParams);

  const formatDt = useCallback(
    (value?: string | null) =>
      value
        ? formatDateTime(value, formatLocale, { dateStyle: 'short', timeStyle: 'short' })
        : FORMAT_EMPTY_DISPLAY,
    [formatLocale],
  );

  const formatMoney = useCallback(
    (value?: number | null) => formatCurrency(value ?? 0, formatLocale),
    [formatLocale],
  );

  const renderStatus = useCallback(
    (status: string) => (
      <Tag color={statusTagColor(status)}>{ts(`status.${status}`) || status}</Tag>
    ),
    [ts],
  );

  const shiftColumns: ColumnsType<AdminShiftRow> = useMemo(
    () => [
      {
        title: ts('columns.cashier'),
        dataIndex: 'cashierName',
        key: 'cashierName',
        ellipsis: true,
      },
      {
        title: ts('columns.register'),
        dataIndex: 'registerNumber',
        key: 'registerNumber',
        width: 120,
        render: (v: string | null | undefined, row) => v || row.cashRegisterId,
      },
      {
        title: ts('columns.startedAt'),
        dataIndex: 'startedAt',
        key: 'startedAt',
        width: 160,
        render: formatDt,
      },
      {
        title: ts('columns.endedAt'),
        dataIndex: 'endedAt',
        key: 'endedAt',
        width: 160,
        render: formatDt,
      },
      {
        title: ts('columns.sales'),
        dataIndex: 'totalSales',
        key: 'totalSales',
        align: 'right',
        render: formatMoney,
      },
      {
        title: ts('columns.cash'),
        dataIndex: 'totalCash',
        key: 'totalCash',
        align: 'right',
        render: formatMoney,
      },
      {
        title: ts('columns.card'),
        dataIndex: 'totalCard',
        key: 'totalCard',
        align: 'right',
        render: formatMoney,
      },
      {
        title: ts('columns.cashCount'),
        dataIndex: 'cashCount',
        key: 'cashCount',
        align: 'right',
        render: (v: number | null | undefined) => (v == null ? FORMAT_EMPTY_DISPLAY : formatMoney(v)),
      },
      {
        title: ts('columns.difference'),
        dataIndex: 'difference',
        key: 'difference',
        align: 'right',
        render: formatMoney,
      },
      {
        title: ts('columns.status'),
        dataIndex: 'status',
        key: 'status',
        width: 130,
        render: renderStatus,
      },
    ],
    [formatDt, formatMoney, renderStatus, ts],
  );

  const historyColumns: ColumnsType<AdminShiftRow> = useMemo(
    () => [
      ...shiftColumns.slice(0, 4),
      {
        title: ts('columns.startBalance'),
        dataIndex: 'startBalance',
        key: 'startBalance',
        align: 'right',
        render: formatMoney,
      },
      {
        title: ts('columns.endBalance'),
        dataIndex: 'endBalance',
        key: 'endBalance',
        align: 'right',
        render: formatMoney,
      },
      ...shiftColumns.slice(4),
    ],
    [formatMoney, shiftColumns, ts],
  );

  const closingColumns: ColumnsType<AdminDailyClosingOverviewRow> = useMemo(
    () => [
      {
        title: ts('columns.closingDate'),
        dataIndex: 'closingDate',
        key: 'closingDate',
        width: 160,
        render: formatDt,
      },
      {
        title: ts('columns.cashier'),
        dataIndex: 'cashierName',
        key: 'cashierName',
        ellipsis: true,
      },
      {
        title: ts('columns.register'),
        dataIndex: 'registerNumber',
        key: 'registerNumber',
        width: 120,
        render: (v: string | null | undefined, row) => v || row.cashRegisterId,
      },
      {
        title: ts('columns.sales'),
        dataIndex: 'totalSales',
        key: 'totalSales',
        align: 'right',
        render: formatMoney,
      },
      {
        title: ts('columns.cashCount'),
        dataIndex: 'cashCount',
        key: 'cashCount',
        align: 'right',
        render: (v: number | null | undefined) => (v == null ? FORMAT_EMPTY_DISPLAY : formatMoney(v)),
      },
      {
        title: ts('columns.difference'),
        dataIndex: 'difference',
        key: 'difference',
        align: 'right',
        render: formatMoney,
      },
      {
        title: ts('columns.fiscalTotal'),
        dataIndex: 'fiscalTotalAmount',
        key: 'fiscalTotalAmount',
        align: 'right',
        render: formatMoney,
      },
      {
        title: ts('columns.transactions'),
        dataIndex: 'fiscalTransactionCount',
        key: 'fiscalTransactionCount',
        align: 'right',
      },
      {
        title: ts('columns.tse'),
        dataIndex: 'hasTseSignature',
        key: 'hasTseSignature',
        width: 110,
        render: (v: boolean) => (
          <Tag color={v ? 'green' : 'red'}>{v ? ts('tse.yes') : ts('tse.no')}</Tag>
        ),
      },
      {
        title: ts('columns.status'),
        dataIndex: 'shiftStatus',
        key: 'shiftStatus',
        width: 130,
        render: renderStatus,
      },
    ],
    [formatDt, formatMoney, renderStatus, ts],
  );

  const loadError = overviewQ.isError
    ? getUserFacingApiErrorMessage(overviewQ.error, ts('errors.loadFailed'))
    : null;

  return (
    <>
      <AdminPageHeader
        title={ts('pageTitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: ts('pageTitle'), href: '/shifts' },
        ]}
      />
      <AdminPageShell>
        <Card>
          <Typography.Paragraph type="secondary">{ts('intro')}</Typography.Paragraph>
          <Space wrap style={{ marginBottom: 16 }}>
            <span>{ts('filters.register')}</span>
            <Select
              allowClear
              placeholder={ts('filters.allRegisters')}
              style={{ minWidth: 220 }}
              value={registerId}
              onChange={(v) => setRegisterId(v)}
              loading={registersQ.isLoading}
              options={registerRows.map((r) => ({
                value: r.id,
                label: r.registerNumber
                  ? `${r.registerNumber} (${r.location ?? ''})`
                  : String(r.id),
              }))}
            />
            <span>{ts('filters.from')}</span>
            <DatePicker
              value={fromDay}
              onChange={setFromDay}
              format="DD.MM.YYYY"
              allowClear
            />
            <span>{ts('filters.to')}</span>
            <DatePicker value={toDay} onChange={setToDay} format="DD.MM.YYYY" allowClear />
            <Button
              icon={<ReloadOutlined />}
              onClick={() => void overviewQ.refetch()}
              loading={overviewQ.isFetching}
            >
              {ts('filters.refresh')}
            </Button>
            <Link href="/tagesabschluss">{t('nav.tagesabschluss')}</Link>
            <Link href="/kassenverwaltung">{t('nav.kassenverwaltung')}</Link>
          </Space>

          {loadError ? <Alert type="error" showIcon title={loadError} style={{ marginBottom: 16 }} /> : null}

          <Card type="inner" title={ts('sections.active')} style={{ marginBottom: 16 }}>
            <Table<AdminShiftRow>
              rowKey="id"
              size="small"
              loading={overviewQ.isLoading}
              dataSource={overviewQ.data?.activeShifts ?? []}
              columns={shiftColumns}
              pagination={false}
              scroll={{ x: 1100 }}
              locale={{ emptyText: ts('empty') }}
            />
          </Card>

          <Card type="inner" title={ts('sections.history')} style={{ marginBottom: 16 }}>
            <Table<AdminShiftRow>
              rowKey="id"
              size="small"
              loading={overviewQ.isLoading}
              dataSource={overviewQ.data?.shiftHistory ?? []}
              columns={historyColumns}
              pagination={{ pageSize: 20, showSizeChanger: true }}
              scroll={{ x: 1300 }}
              locale={{ emptyText: ts('empty') }}
            />
          </Card>

          <Card type="inner" title={ts('sections.closings')}>
            <Table<AdminDailyClosingOverviewRow>
              rowKey="dailyClosingId"
              size="small"
              loading={overviewQ.isLoading}
              dataSource={overviewQ.data?.dailyClosings ?? []}
              columns={closingColumns}
              pagination={{ pageSize: 20, showSizeChanger: true }}
              scroll={{ x: 1200 }}
              locale={{ emptyText: ts('empty') }}
            />
          </Card>
        </Card>
      </AdminPageShell>
    </>
  );
};
