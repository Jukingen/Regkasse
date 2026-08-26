'use client';

import { EyeOutlined } from '@ant-design/icons';
import { Button, Table, Tag } from 'antd';
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table';
import { useMemo } from 'react';

import { dateColumnRender } from '@/components/DateColumn';
import { adminTableScrollXy } from '@/components/ui/adminTableVirtual';
import type { AdminOnlinePaymentDto } from '@/features/online-payments/api/onlinePaymentsApi';
import { useI18n } from '@/i18n';
import { formatCurrency } from '@/i18n/formatting';

const STATUS_COLORS: Record<string, string> = {
  PENDING: 'processing',
  AWAITING_PAYMENT_GATEWAY: 'processing',
  GATEWAY_SUCCEEDED: 'success',
  Succeeded: 'success',
  COMPLETED: 'success',
  FAILED: 'error',
  REFUNDED: 'purple',
  Created: 'blue',
  Pending: 'processing',
  Failed: 'error',
  Cancelled: 'default',
  Expired: 'warning',
  Refunded: 'purple',
};

type OnlinePaymentsTableProps = {
  payments: AdminOnlinePaymentDto[];
  loading?: boolean;
  emptyText?: string;
  pagination?: TablePaginationConfig;
  onViewDetails: (row: AdminOnlinePaymentDto) => void;
};

export function OnlinePaymentsTable({
  payments,
  loading = false,
  emptyText,
  pagination,
  onViewDetails,
}: OnlinePaymentsTableProps) {
  const { t, formatLocale } = useI18n();
  const ts = (key: string) => t(`onlinePayments.${key}`);

  const columns: ColumnsType<AdminOnlinePaymentDto> = useMemo(
    () => [
      {
        title: ts('columns.createdAt'),
        dataIndex: 'createdAtUtc',
        key: 'createdAtUtc',
        width: 170,
        render: dateColumnRender('datetime'),
      },
      {
        title: ts('columns.tenant'),
        key: 'tenant',
        width: 180,
        ellipsis: true,
        render: (_, row) => row.tenantName || row.tenantSlug || row.tenantId,
      },
      {
        title: ts('columns.amount'),
        dataIndex: 'amount',
        key: 'amount',
        width: 120,
        render: (amount: number, row) =>
          formatCurrency(amount, formatLocale, { currency: row.currency }),
      },
      {
        title: ts('columns.status'),
        dataIndex: 'status',
        key: 'status',
        width: 140,
        render: (status: string, row) => (
          <>
            <Tag color={STATUS_COLORS[status] ?? 'default'}>
              {t(`onlinePayments.status.${status}`)}
            </Tag>
            {row.isSynthetic ? <Tag>{ts('source.synthetic')}</Tag> : null}
          </>
        ),
      },
      {
        title: ts('columns.method'),
        dataIndex: 'paymentMethod',
        key: 'paymentMethod',
        width: 120,
      },
      {
        title: ts('columns.actions'),
        key: 'actions',
        width: 150,
        fixed: 'right',
        render: (_, row) => (
          <Button
            type="link"
            size="small"
            icon={<EyeOutlined />}
            onClick={(e) => {
              e.stopPropagation();
              onViewDetails(row);
            }}
          >
            {ts('actions.viewDetails')}
          </Button>
        ),
      },
    ],
    [formatLocale, onViewDetails, t, ts]
  );

  return (
    <Table<AdminOnlinePaymentDto>
      rowKey="id"
      size="small"
      loading={loading}
      dataSource={payments}
      columns={columns}
      scroll={adminTableScrollXy(900, 480)}
      pagination={pagination ?? false}
      locale={{ emptyText: emptyText ?? ts('empty') }}
      onRow={(row) => ({
        onClick: () => onViewDetails(row),
      })}
    />
  );
}
