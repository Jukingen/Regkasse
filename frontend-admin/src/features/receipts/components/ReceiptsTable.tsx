'use client';

import React from 'react';
import { Table, Button, Tag } from 'antd';
import { EyeOutlined } from '@ant-design/icons';
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table';
import type { SorterResult } from 'antd/es/table/interface';
import type { ReceiptListItemDto } from '@/features/receipts/types/receipts';
import { formatEUR } from '@/shared/utils/currency';
import dayjs from 'dayjs';
import Link from 'next/link';

interface ReceiptsTableProps {
    data: ReceiptListItemDto[];
    loading: boolean;
    isPlaceholderData: boolean;
    pagination: {
        current: number;
        pageSize: number;
        total: number;
    };
    sortField?: string;
    sortOrder?: 'ascend' | 'descend';
    onTableChange: (
        pagination: TablePaginationConfig,
        filters: Record<string, any>,
        sorter: SorterResult<ReceiptListItemDto> | SorterResult<ReceiptListItemDto>[],
    ) => void;
}

const columns: ColumnsType<ReceiptListItemDto> = [
    {
        title: 'Receipt No.',
        dataIndex: 'receiptNumber',
        key: 'receiptNumber',
        render: (text: string) => <span style={{ fontWeight: 600 }}>{text}</span>,
    },
    {
        title: 'Issued At',
        dataIndex: 'issuedAt',
        key: 'issuedAt',
        sorter: true,
        render: (date: string) => dayjs(date).format('DD.MM.YYYY HH:mm'),
    },
    {
        title: 'Cash Register',
        dataIndex: 'cashRegisterId',
        key: 'cashRegisterId',
        render: (text: string) => <Tag>{text}</Tag>,
    },
    {
        title: 'Cashier',
        dataIndex: 'cashierId',
        key: 'cashierId',
        render: (text: string | null) => text || '—',
    },
    {
        title: 'Grand Total',
        dataIndex: 'grandTotal',
        key: 'grandTotal',
        align: 'right',
        sorter: true,
        render: (val: number) => formatEUR(val),
    },
    {
        title: 'Actions',
        key: 'actions',
        width: 100,
        render: (_: unknown, record: ReceiptListItemDto) => (
            <Link href={`/receipts/${record.receiptId}`}>
                <Button size="small" icon={<EyeOutlined />}>
                    View
                </Button>
            </Link>
        ),
    },
];

/**
 * Paginated, sortable receipts table.
 * Purely presentational — all data and pagination state comes from props.
 */
export default function ReceiptsTable({
    data,
    loading,
    isPlaceholderData,
    pagination,
    sortField,
    sortOrder,
    onTableChange,
}: ReceiptsTableProps) {
    // Apply current sort indicator to the matching column
    const columnsWithSort = columns.map((col) => {
        if ('dataIndex' in col && col.dataIndex === sortField) {
            return { ...col, sortOrder: sortOrder };
        }
        return col;
    });

    return (
        <Table<ReceiptListItemDto>
            columns={columnsWithSort}
            dataSource={data}
            rowKey="receiptId"
            loading={loading && !isPlaceholderData}
            onChange={onTableChange}
            pagination={{
                current: pagination.current,
                pageSize: pagination.pageSize,
                total: pagination.total,
                showSizeChanger: true,
                pageSizeOptions: ['10', '25', '50', '100'],
                showTotal: (total, range) => `${range[0]}–${range[1]} of ${total} receipts`,
            }}
            style={{
                opacity: isPlaceholderData ? 0.6 : 1,
                transition: 'opacity 0.2s',
            }}
            size="middle"
        />
    );
}
