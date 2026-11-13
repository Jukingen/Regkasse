'use client';

import { useQuery } from '@tanstack/react-query';
import { Alert, Table, Tabs, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo } from 'react';

import { getApiAuditLog } from '@/api/generated/audit-log/audit-log';
import type { AdminPaymentListItemDto } from '@/api/generated/model';
import type { AuditLogEntryDto } from '@/api/generated/model/auditLogEntryDto';
import { fetchAdminPaymentsList } from '@/features/payments/api/adminPaymentsListQuery';
import { getReceiptListForensics } from '@/features/receipts/api/forensics-client';
import { formatRksvSpecialReceiptKindDisplay } from '@/features/receipts/utils/formatRksvSpecialReceiptKind';
import {
  type AdminShiftRow,
  fetchAdminShiftOverview,
} from '@/features/shifts/api/shiftsOverview';
import { ShiftHistoryTable } from '@/features/shifts/components/ShiftHistoryTable';
import { FORMAT_EMPTY_DISPLAY, formatCurrency, formatDateTime, useI18n } from '@/i18n';

export type CashRegisterDetailHistoryProps = {
  registerId: string;
};

export function CashRegisterDetailHistory({ registerId }: CashRegisterDetailHistoryProps) {
  const { t, formatLocale } = useI18n();

  const shiftsQuery = useQuery({
    queryKey: ['admin', 'cash-registers', registerId, 'shifts'],
    queryFn: () => fetchAdminShiftOverview({ cashRegisterId: registerId, limit: 50 }),
    enabled: Boolean(registerId),
    staleTime: 15_000,
  });

  const paymentsQuery = useQuery({
    queryKey: ['admin', 'cash-registers', registerId, 'payments'],
    queryFn: () =>
      fetchAdminPaymentsList({
        cashRegisterId: registerId,
        page: 1,
        pageNumber: 1,
        pageSize: 25,
        sortBy: 'CreatedAt',
        sortDirection: 'desc',
        includeTotalCount: true,
      }),
    enabled: Boolean(registerId),
    staleTime: 15_000,
  });

  const receiptsQuery = useQuery({
    queryKey: ['admin', 'cash-registers', registerId, 'receipts'],
    queryFn: () =>
      getReceiptListForensics({
        page: 1,
        pageSize: 50,
        sort: 'issuedAt:desc',
        cashRegisterId: registerId,
      }),
    enabled: Boolean(registerId),
    staleTime: 15_000,
  });

  const auditQuery = useQuery({
    queryKey: ['admin', 'cash-registers', registerId, 'audit'],
    queryFn: () =>
      getApiAuditLog({
        entityType: 'CashRegister',
        entityId: registerId,
        page: 1,
        pageSize: 25,
        includeTotalCount: true,
      }),
    enabled: Boolean(registerId),
    staleTime: 15_000,
  });

  const shiftRows: AdminShiftRow[] = useMemo(
    () => [...(shiftsQuery.data?.activeShifts ?? []), ...(shiftsQuery.data?.shiftHistory ?? [])],
    [shiftsQuery.data]
  );

  const specialReceipts = useMemo(
    () =>
      (receiptsQuery.data?.items ?? []).filter((row) => Boolean(row.rksvSpecialReceiptKind?.trim())),
    [receiptsQuery.data?.items]
  );

  const paymentColumns: ColumnsType<AdminPaymentListItemDto> = [
    {
      title: t('receipts.table.colReceipt'),
      dataIndex: 'receiptNumber',
      render: (value: string | null | undefined) => value?.trim() || FORMAT_EMPTY_DISPLAY,
    },
    {
      title: t('cashRegisters.detail.createdAt'),
      dataIndex: 'createdAt',
      render: (value: string | undefined) =>
        value ? formatDateTime(value, formatLocale) : FORMAT_EMPTY_DISPLAY,
    },
    {
      title: t('payments.table.colMethod'),
      dataIndex: 'method',
      render: (value: string | null | undefined) => value?.trim() || FORMAT_EMPTY_DISPLAY,
    },
    {
      title: t('cashRegisters.detail.statRevenue'),
      dataIndex: 'totalAmount',
      render: (value: number | undefined) => formatCurrency(value ?? 0, formatLocale),
    },
    {
      title: t('cashRegisters.detail.status'),
      dataIndex: 'status',
      render: (value: string | null | undefined) => value?.trim() || FORMAT_EMPTY_DISPLAY,
    },
  ];

  const specialColumns: ColumnsType<(typeof specialReceipts)[number]> = [
    {
      title: t('receipts.table.colReceipt'),
      dataIndex: 'receiptNumber',
    },
    {
      title: t('cashRegisters.detail.createdAt'),
      dataIndex: 'issuedAt',
      render: (value: string) =>
        value ? formatDateTime(value, formatLocale) : FORMAT_EMPTY_DISPLAY,
    },
    {
      title: t('cashRegisters.actions.specialReceipts'),
      dataIndex: 'rksvSpecialReceiptKind',
      render: (value: string | null | undefined) =>
        formatRksvSpecialReceiptKindDisplay(t, value),
    },
    {
      title: t('cashRegisters.detail.statRevenue'),
      dataIndex: 'grandTotal',
      render: (value: number) => formatCurrency(value ?? 0, formatLocale),
    },
  ];

  const auditColumns: ColumnsType<AuditLogEntryDto> = [
    {
      title: t('cashRegisters.detail.createdAt'),
      dataIndex: 'createdAt',
      render: (value: string | undefined) =>
        value ? formatDateTime(value, formatLocale) : FORMAT_EMPTY_DISPLAY,
    },
    {
      title: t('common.auditLogs.actionPlaceholder'),
      dataIndex: 'action',
      render: (_: unknown, row) =>
        row.action?.trim() || row.description?.trim() || FORMAT_EMPTY_DISPLAY,
    },
    {
      title: t('common.auditLogs.userPlaceholder'),
      dataIndex: 'actorDisplayName',
      render: (value: string | null | undefined) => value?.trim() || FORMAT_EMPTY_DISPLAY,
    },
  ];

  return (
    <Tabs
      items={[
        {
          key: 'shifts',
          label: t('cashRegisters.detail.historyShifts'),
          children: shiftsQuery.error ? (
            <Alert type="error" showIcon title={t('cashRegisters.errors.loadFailed')} />
          ) : (
            <ShiftHistoryTable
              rows={shiftRows}
              loading={shiftsQuery.isLoading}
              serverRegisterId={registerId}
            />
          ),
        },
        {
          key: 'payments',
          label: t('cashRegisters.detail.historyPayments'),
          children: paymentsQuery.error ? (
            <Alert type="error" showIcon title={t('cashRegisters.errors.loadFailed')} />
          ) : (
            <Table<AdminPaymentListItemDto>
              size="small"
              rowKey={(row) => row.id ?? row.transactionId ?? String(row.createdAt)}
              loading={paymentsQuery.isLoading}
              columns={paymentColumns}
              dataSource={paymentsQuery.data?.items ?? []}
              pagination={false}
            />
          ),
        },
        {
          key: 'sonderbelege',
          label: t('cashRegisters.detail.historySonderbelege'),
          children: receiptsQuery.error ? (
            <Alert type="error" showIcon title={t('cashRegisters.errors.loadFailed')} />
          ) : (
            <Table
              size="small"
              rowKey={(row) => row.receiptId}
              loading={receiptsQuery.isLoading}
              columns={specialColumns}
              dataSource={specialReceipts}
              pagination={false}
              locale={{
                emptyText: (
                  <Typography.Text type="secondary">
                    {t('cashRegisters.empty')}
                  </Typography.Text>
                ),
              }}
            />
          ),
        },
        {
          key: 'audit',
          label: t('cashRegisters.detail.historyAudit'),
          children: auditQuery.error ? (
            <Alert type="error" showIcon title={t('cashRegisters.errors.loadFailed')} />
          ) : (
            <Table<AuditLogEntryDto>
              size="small"
              rowKey={(row) =>
                row.correlationId ?? `${row.createdAt ?? ''}-${row.action ?? ''}-${row.entityId ?? ''}`
              }
              loading={auditQuery.isLoading}
              columns={auditColumns}
              dataSource={auditQuery.data?.auditLogs ?? []}
              pagination={false}
            />
          ),
        },
      ]}
    />
  );
}
