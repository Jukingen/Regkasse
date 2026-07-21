'use client';

import { Table } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import React, { useMemo } from 'react';

import type { ReceiptItemDto } from '@/features/receipts/types/receipts';
import { useI18n } from '@/i18n';
import { formatEUR } from '@/shared/utils/currency';

interface ReceiptItemsTableProps {
  items: ReceiptItemDto[];
}

/**
 * Basit tablo: fis satirlari.
 */
export default function ReceiptItemsTable({ items }: ReceiptItemsTableProps) {
  const { t } = useI18n();

  const columns: ColumnsType<ReceiptItemDto> = useMemo(
    () => [
      {
        title: t('receipts.detail.itemsTable.colProduct'),
        dataIndex: 'productName',
        key: 'productName',
      },
      {
        title: t('receipts.detail.itemsTable.colQty'),
        dataIndex: 'quantity',
        key: 'quantity',
        align: 'center',
        width: 80,
      },
      {
        title: t('receipts.detail.itemsTable.colUnitPrice'),
        dataIndex: 'unitPrice',
        key: 'unitPrice',
        align: 'right',
        render: (val: number) => formatEUR(val),
      },
      {
        title: t('receipts.detail.itemsTable.colTaxRate'),
        dataIndex: 'taxRate',
        key: 'taxRate',
        align: 'center',
        width: 100,
        render: (val: number) => `${val}%`,
      },
      {
        title: t('receipts.detail.itemsTable.colTotal'),
        dataIndex: 'totalPrice',
        key: 'totalPrice',
        align: 'right',
        render: (val: number) => formatEUR(val),
      },
    ],
    [t]
  );

  return (
    <Table<ReceiptItemDto>
      columns={columns}
      dataSource={items}
      rowKey="itemId"
      pagination={false}
      size="small"
    />
  );
}
