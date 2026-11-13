'use client';

import { useQuery } from '@tanstack/react-query';
import { Button, Card, Input, Space, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo, useState } from 'react';

import { VirtualTable } from '@/components/VirtualTable';
import { adminTablePaginationDefaults } from '@/components/ui/adminTablePagination';
import { dateColumnRender } from '@/components/DateColumn';
import {
  adminPreorderQueryKeys,
  fetchAdminPreorders,
  type AdminPreorder,
  type PreorderStatus,
} from '@/features/orders/api/preordersApi';
import { useI18n } from '@/i18n';

const FILTERS: Array<PreorderStatus | ''> = ['', 'pending', 'ready', 'collected', 'cancelled'];

function statusColor(status: string): string {
  switch (status) {
    case 'ready':
      return 'blue';
    case 'collected':
      return 'green';
    case 'cancelled':
      return 'red';
    default:
      return 'orange';
  }
}

export function PreorderList() {
  const { t, formatLocale } = useI18n();
  const [status, setStatus] = useState<PreorderStatus | ''>('');
  const [receiptNumber, setReceiptNumber] = useState('');
  const [appliedReceipt, setAppliedReceipt] = useState('');

  const query = useQuery({
    queryKey: adminPreorderQueryKeys.list(status, appliedReceipt),
    queryFn: () =>
      fetchAdminPreorders({
        status: status || undefined,
        receiptNumber: appliedReceipt || undefined,
      }),
  });

  const statusLabel = (value: string) => {
    switch (value) {
      case 'ready':
        return t('onlineOrders.preorder.statusReady');
      case 'collected':
        return t('onlineOrders.preorder.statusCollected');
      case 'cancelled':
        return t('onlineOrders.preorder.statusCancelled');
      default:
        return t('onlineOrders.preorder.statusPending');
    }
  };

  const columns: ColumnsType<AdminPreorder> = useMemo(
    () => [
      {
        title: t('onlineOrders.preorder.columnNumber'),
        dataIndex: 'preorderNumber',
        render: (value: string, row) => value || row.orderId,
      },
      {
        title: t('onlineOrders.preorder.columnReceipt'),
        dataIndex: 'receiptNumber',
        render: (value: string, row) => value || row.orderId,
      },
      {
        title: t('onlineOrders.preorder.columnCustomer'),
        dataIndex: 'customerName',
      },
      {
        title: t('onlineOrders.preorder.columnStatus'),
        dataIndex: 'status',
        render: (value: string) => <Tag color={statusColor(value)}>{statusLabel(value)}</Tag>,
      },
      {
        title: t('onlineOrders.preorder.columnPaid'),
        dataIndex: 'paidAmount',
        render: (value: number, row) =>
          new Intl.NumberFormat(formatLocale, { style: 'currency', currency: 'EUR' }).format(
            value ?? row.totalAmount ?? 0
          ),
      },
      {
        title: t('onlineOrders.preorder.columnOpen'),
        dataIndex: 'remainingAmount',
        render: (value: number) =>
          new Intl.NumberFormat(formatLocale, { style: 'currency', currency: 'EUR' }).format(
            value ?? 0
          ),
      },
      {
        title: t('onlineOrders.preorder.columnDate'),
        dataIndex: 'orderDate',
        render: dateColumnRender('datetime'),
      },
    ],
    [formatLocale, t]
  );

  return (
    <Card>
      <Space wrap style={{ marginBottom: 16 }}>
        {FILTERS.map((id) => (
          <Button
            key={id || 'all'}
            type={status === id ? 'primary' : 'default'}
            onClick={() => setStatus(id)}>
            {id === ''
              ? t('onlineOrders.preorder.filterAll')
              : statusLabel(id)}
          </Button>
        ))}
        <Input
          value={receiptNumber}
          onChange={(e) => setReceiptNumber(e.target.value)}
          placeholder={t('onlineOrders.preorder.searchPlaceholder')}
          style={{ width: 220 }}
          allowClear
        />
        <Button type="primary" onClick={() => setAppliedReceipt(receiptNumber.trim())}>
          {t('onlineOrders.preorder.search')}
        </Button>
      </Space>
      <VirtualTable
        rowKey="id"
        loading={query.isLoading}
        dataSource={query.data?.orders ?? []}
        columns={columns}
        locale={{ emptyText: t('onlineOrders.preorder.empty') }}
        pagination={adminTablePaginationDefaults}
      />
    </Card>
  );
}
