'use client';

import React from 'react';
import { Table } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { ReceiptItemDto } from '@/features/receipts/types/receipts';
import { formatEUR } from '@/shared/utils/currency';

interface ReceiptItemsTableProps {
    items: ReceiptItemDto[];
}

const columns: ColumnsType<ReceiptItemDto> = [
    {
        title: 'Product',
        dataIndex: 'productName',
        key: 'productName',
    },
    {
        title: 'Qty',
        dataIndex: 'quantity',
        key: 'quantity',
        align: 'center',
        width: 80,
    },
    {
        title: 'Unit Price',
        dataIndex: 'unitPrice',
        key: 'unitPrice',
        align: 'right',
        render: (val: number) => formatEUR(val),
    },
    {
        title: 'Tax Rate',
        dataIndex: 'taxRate',
        key: 'taxRate',
        align: 'center',
        width: 100,
        render: (val: number) => `${val}%`,
    },
    {
        title: 'Total',
        dataIndex: 'totalPrice',
        key: 'totalPrice',
        align: 'right',
        render: (val: number) => formatEUR(val),
    },
];

/**
 * Simple table displaying line items within a receipt.
 */
export default function ReceiptItemsTable({ items }: ReceiptItemsTableProps) {
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
