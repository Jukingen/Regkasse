'use client';

import { ReloadOutlined } from '@ant-design/icons';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, DatePicker, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs, { type Dayjs } from 'dayjs';
import Link from 'next/link';
import React, { useCallback, useMemo, useState } from 'react';

import { CashRegisterSelector } from '@/components/CashRegisterSelector';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { invalidateShiftRelatedQueries } from '@/features/shifts/api/shiftQueryInvalidation';
import {
  type AdminDailyClosingOverviewRow,
  type AdminShiftRow,
  forceCloseAdminShiftRegister,
} from '@/features/shifts/api/shiftsOverview';
import { useAdminShiftOverview } from '@/features/shifts/hooks/useAdminShiftOverview';
import { useAntdApp } from '@/hooks/useAntdApp';
import { usePermissions } from '@/hooks/usePermissions';
import { FORMAT_EMPTY_DISPLAY, formatCurrency, formatDateTime, useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { AppPermissions, PERMISSIONS } from '@/shared/auth/permissions';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';

const STALE_SHIFT_WARNING_HOURS = 8;

function statusTagColor(status: string): string {
  switch (status) {
    case 'Active':
    case 'RegisterOpen':
      return 'green';
    case 'Discrepancy':
      return 'orange';
    case 'Completed':
    default:
      return 'blue';
  }
}

export type ShiftOverviewProps = {
  /** When rendered under `/staff/shifts`, use staff hub breadcrumbs and back link. */
  staffHubMode?: boolean;
};

export const ShiftOverview: React.FC<ShiftOverviewProps> = ({ staffHubMode = false }) => {
  const { t, formatLocale } = useI18n();
  const { modal, message } = useAntdApp();
  const { hasPermission } = usePermissions();
  const queryClient = useQueryClient();
  const canForceClose = hasPermission(PERMISSIONS.SHIFT_MANAGE);
  const ts = useCallback((path: string) => t(`shifts:${path}`), [t]);

  const [registerId, setRegisterId] = useState<string | undefined>();
  const [fromDay, setFromDay] = useState<Dayjs | null>(null);
  const [toDay, setToDay] = useState<Dayjs | null>(null);

  const queryParams = useMemo(
    () => ({
      cashRegisterId: registerId,
      fromUtc: fromDay ? fromDay.startOf('day').toISOString() : undefined,
      toUtc: toDay ? toDay.add(1, 'day').startOf('day').toISOString() : undefined,
      limit: 200,
    }),
    [fromDay, registerId, toDay]
  );

  const overviewQ = useAdminShiftOverview(queryParams);

  const forceCloseMutation = useMutation({
    mutationFn: (row: AdminShiftRow) =>
      forceCloseAdminShiftRegister(row.cashRegisterId, {
        reason: row.isOrphanedRegisterSession
          ? 'Orphaned register session'
          : 'Manual admin force-close',
      }),
    onSuccess: async (_data, row) => {
      message.success(ts('actions.forceCloseSuccess'));
      await invalidateShiftRelatedQueries(queryClient, row.cashRegisterId);
    },
    onError: (err) => {
      message.error(
        getUserFacingApiErrorMessage(t, err, {
          logContext: 'ShiftOverview.forceClose',
          fallbackKey: 'shifts:actions.forceCloseFailed',
        })
      );
    },
  });

  const handleForceClose = useCallback(
    (row: AdminShiftRow) => {
      modal.confirm({
        title: ts('actions.forceCloseTitle'),
        content: ts('actions.forceCloseConfirm'),
        okText: ts('actions.forceClose'),
        okButtonProps: { danger: true },
        onOk: () => forceCloseMutation.mutateAsync(row),
      });
    },
    [forceCloseMutation, modal, ts]
  );

  const activeShifts = overviewQ.data?.activeShifts ?? [];
  const staleActiveShifts = activeShifts.filter(
    (row) => (row.openDurationHours ?? 0) >= STALE_SHIFT_WARNING_HOURS
  );

  const formatDt = useCallback(
    (value?: string | null) =>
      value
        ? formatDateTime(value, formatLocale, { dateStyle: 'short', timeStyle: 'short' })
        : FORMAT_EMPTY_DISPLAY,
    [formatLocale]
  );

  const formatMoney = useCallback(
    (value?: number | null) => formatCurrency(value ?? 0, formatLocale),
    [formatLocale]
  );

  const renderStatus = useCallback(
    (status: string, row: AdminShiftRow) => (
      <Space size={4} wrap>
        <Tag color={statusTagColor(status)}>{ts(`status.${status}`) || status}</Tag>
        {row.isOrphanedRegisterSession ? (
          <Tag color="gold">{ts('badges.orphanedRegister')}</Tag>
        ) : null}
        {(row.openDurationHours ?? 0) >= STALE_SHIFT_WARNING_HOURS ? (
          <Tag color="orange">{ts('badges.staleShift')}</Tag>
        ) : null}
      </Space>
    ),
    [ts]
  );

  const shiftColumns: ColumnsType<AdminShiftRow> = useMemo(() => {
    const columns: ColumnsType<AdminShiftRow> = [
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
        render: (v: number | null | undefined) =>
          v == null ? FORMAT_EMPTY_DISPLAY : formatMoney(v),
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
        width: 220,
        render: (status: string, row) => renderStatus(status, row),
      },
    ];

    if (canForceClose) {
      columns.push({
        title: ts('columns.actions'),
        key: 'actions',
        width: 180,
        render: (_value, row) => (
          <Button
            size="small"
            danger
            loading={forceCloseMutation.isPending}
            onClick={() => handleForceClose(row)}
          >
            {ts('actions.forceClose')}
          </Button>
        ),
      });
    }

    return columns;
  }, [
    canForceClose,
    forceCloseMutation.isPending,
    formatDt,
    formatMoney,
    handleForceClose,
    renderStatus,
    ts,
  ]);

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
      ...shiftColumns.slice(4, -(canForceClose ? 1 : 0)),
    ],
    [canForceClose, formatMoney, shiftColumns, ts]
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
        render: (v: number | null | undefined) =>
          v == null ? FORMAT_EMPTY_DISPLAY : formatMoney(v),
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
        render: (status: string) => (
          <Tag color={statusTagColor(status)}>{ts(`status.${status}`) || status}</Tag>
        ),
      },
    ],
    [formatDt, formatMoney, ts]
  );

  const loadError = overviewQ.isError
    ? getUserFacingApiErrorMessage(t, overviewQ.error, {
        logContext: 'ShiftOverview.load',
        fallbackKey: 'shifts:errors.loadFailed',
      })
    : null;

  return (
    <>
      <AdminPageHeader
        title={ts('pageTitle')}
        breadcrumbs={
          staffHubMode
            ? [
                adminOverviewCrumb(t),
                { title: t('staff:hub.pageTitle'), href: '/staff' },
                { title: t('staff:nav.shifts') },
              ]
            : [adminOverviewCrumb(t), { title: ts('pageTitle'), href: '/shifts' }]
        }
      />
      <AdminPageShell>
        <Card>
          <Typography.Paragraph type="secondary">{ts('intro')}</Typography.Paragraph>
          {staffHubMode ? (
            <Typography.Paragraph style={{ marginBottom: 16 }}>
              <Link href="/staff">{t('staff:hub.backLink')}</Link>
            </Typography.Paragraph>
          ) : null}
          <Space wrap style={{ marginBottom: 16 }}>
            <span>{ts('filters.register')}</span>
            <CashRegisterSelector
              value={registerId}
              onChange={setRegisterId}
              showFormItem={false}
              required={false}
              allowClear
              placeholder={ts('filters.allRegisters')}
              style={{ minWidth: 220 }}
            />
            <span>{ts('filters.from')}</span>
            <DatePicker value={fromDay} onChange={setFromDay} format="DD.MM.YYYY" allowClear />
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

          {loadError ? (
            <Alert type="error" showIcon title={loadError} style={{ marginBottom: 16 }} />
          ) : null}

          {staleActiveShifts.length > 0 ? (
            <Alert
              type="warning"
              showIcon
              title={ts('warnings.staleShiftsTitle')}
              description={t('shifts:warnings.staleShiftsDescription', {
                count: staleActiveShifts.length,
                hours: STALE_SHIFT_WARNING_HOURS,
              })}
              style={{ marginBottom: 16 }}
            />
          ) : null}

          <Card type="inner" title={ts('sections.active')} style={{ marginBottom: 16 }}>
            <Table<AdminShiftRow>
              rowKey={(row) => `${row.id}-${row.cashRegisterId}`}
              size="small"
              loading={overviewQ.isLoading}
              dataSource={activeShifts}
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
