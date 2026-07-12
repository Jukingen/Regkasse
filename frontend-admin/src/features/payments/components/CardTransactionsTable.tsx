'use client';

import React, { useMemo, useState } from 'react';
import Link from 'next/link';
import { Table, Tag, Button, Modal, Descriptions, Typography } from 'antd';
import { EyeOutlined } from '@ant-design/icons';
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table';
import type { AdminCardTransactionRow } from '@/features/payments/api/adminCardTransactionsQuery';
import { useI18n } from '@/i18n';
import { formatCurrency, formatDateTime } from '@/i18n/formatting';
import { adminTableScrollXy } from '@/components/ui/adminTableVirtual';

const STATUS_COLORS: Record<string, string> = {
  Succeeded: 'success',
  Failed: 'error',
  Pending: 'processing',
  Created: 'blue',
  Cancelled: 'warning',
  Refunded: 'purple',
};

export type CardTransactionsTableProps = {
  transactions: AdminCardTransactionRow[];
  loading?: boolean;
  emptyText?: string;
  pagination?: TablePaginationConfig;
};

export function CardTransactionsTable({
  transactions,
  loading = false,
  emptyText,
  pagination,
}: CardTransactionsTableProps) {
  const { t, formatLocale } = useI18n();
  const ts = (key: string, fallback?: string) => {
    const fullKey = `cardTransactions:${key}`;
    const value = t(fullKey);
    return value === fullKey ? (fallback ?? key) : value;
  };
  const [selected, setSelected] = useState<AdminCardTransactionRow | null>(null);

  const columns: ColumnsType<AdminCardTransactionRow> = useMemo(
    () => [
      {
        title: ts('columns.createdAt'),
        dataIndex: 'createdAtUtc',
        key: 'createdAtUtc',
        width: 170,
        render: (value: string) => formatDateTime(value, formatLocale),
      },
      {
        title: ts('columns.amount'),
        dataIndex: 'amount',
        key: 'amount',
        width: 120,
        render: (amount: number, row) => formatCurrency(amount, formatLocale, { currency: row.currency }),
      },
      {
        title: ts('columns.card'),
        key: 'card',
        width: 160,
        render: (_, row) => {
          const brand = row.cardBrand ?? '***';
          const last4 = row.lastFourDigits ?? '****';
          return `${brand} **** ${last4}`;
        },
      },
      {
        title: ts('columns.gateway'),
        dataIndex: 'gatewayProvider',
        key: 'gatewayProvider',
        width: 100,
      },
      {
        title: ts('columns.status'),
        dataIndex: 'status',
        key: 'status',
        width: 130,
        render: (status: string) => (
          <Tag color={STATUS_COLORS[status] ?? 'default'}>{ts(`status.${status}`, status)}</Tag>
        ),
      },
      {
        title: ts('columns.actions'),
        key: 'actions',
        width: 110,
        fixed: 'right',
        render: (_, row) => (
          <Button
            type="link"
            size="small"
            icon={<EyeOutlined />}
            onClick={(e) => {
              e.stopPropagation();
              setSelected(row);
            }}
          >
            {ts('actions.detail')}
          </Button>
        ),
      },
    ],
    [formatLocale, ts],
  );

  return (
    <>
      <Table<AdminCardTransactionRow>
        rowKey="id"
        size="small"
        loading={loading}
        dataSource={transactions}
        columns={columns}
        scroll={adminTableScrollXy(900, 480)}
        pagination={pagination ?? false}
        locale={{ emptyText: emptyText ?? ts('empty') }}
      />

      <Modal
        title={ts('modal.title')}
        open={selected != null}
        onCancel={() => setSelected(null)}
        footer={null}
        width={560}
        destroyOnHidden
      >
        {selected ? (
          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label={ts('columns.amount')}>
              {formatCurrency(selected.amount, formatLocale, { currency: selected.currency })}
            </Descriptions.Item>
            <Descriptions.Item label={ts('columns.status')}>
              <Tag color={STATUS_COLORS[selected.status] ?? 'default'}>
                {ts(`status.${selected.status}`, selected.status)}
              </Tag>
            </Descriptions.Item>
            <Descriptions.Item label={ts('columns.gateway')}>{selected.gatewayProvider}</Descriptions.Item>
            <Descriptions.Item label={ts('columns.transactionId')}>{selected.transactionId ?? '—'}</Descriptions.Item>
            <Descriptions.Item label={ts('columns.card')}>
              {selected.cardBrand && selected.lastFourDigits
                ? `${selected.cardBrand} **** ${selected.lastFourDigits}`
                : '—'}
            </Descriptions.Item>
            <Descriptions.Item label={ts('columns.createdAt')}>
              {formatDateTime(selected.createdAtUtc, formatLocale)}
            </Descriptions.Item>
            {selected.confirmedAtUtc ? (
              <Descriptions.Item label={ts('modal.confirmedAt')}>
                {formatDateTime(selected.confirmedAtUtc, formatLocale)}
              </Descriptions.Item>
            ) : null}
            {selected.errorMessage ? (
              <Descriptions.Item label={ts('modal.error')}>
                <Typography.Text type="danger">{selected.errorMessage}</Typography.Text>
              </Descriptions.Item>
            ) : null}
            {selected.paymentDetailsId ? (
              <Descriptions.Item label={ts('columns.receipt')}>
                <Link href={`/payments/${selected.paymentDetailsId}`}>
                  {selected.receiptNumber ?? selected.paymentDetailsId}
                </Link>
              </Descriptions.Item>
            ) : null}
          </Descriptions>
        ) : null}
      </Modal>
    </>
  );
}
