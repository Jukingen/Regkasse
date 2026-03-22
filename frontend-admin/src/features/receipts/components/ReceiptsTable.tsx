'use client';

import React from 'react';
import { Table, Button, Tag, Typography, Space } from 'antd';
import { EyeOutlined } from '@ant-design/icons';
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table';
import type { FilterValue, SorterResult } from 'antd/es/table/interface';
import type { ReceiptListItemDto } from '@/features/receipts/types/receipts';
import { formatEUR } from '@/shared/utils/currency';
import { formatRegisterDisplayLabel } from '@/shared/utils/registerIdentity';
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
    /** Optional text when table has no data (e.g. "No receipts found. Try adjusting filters.") */
    emptyText?: string;
    onTableChange: (
        pagination: TablePaginationConfig,
        filters: Record<string, FilterValue | null>,
        sorter: SorterResult<ReceiptListItemDto> | SorterResult<ReceiptListItemDto>[],
    ) => void;
}

const columns: ColumnsType<ReceiptListItemDto> = [
    {
        title: 'Receipt',
        dataIndex: 'issuedAt',
        key: 'receipt',
        sorter: true,
        width: 210,
        render: (_: string, row: ReceiptListItemDto) => (
            <Space direction="vertical" size={0}>
                <Typography.Text strong ellipsis style={{ maxWidth: 200, display: 'block' }}>
                    {row.receiptNumber}
                </Typography.Text>
                <Typography.Text
                    type="secondary"
                    style={{ fontSize: 12, fontVariantNumeric: 'tabular-nums' }}
                >
                    {dayjs(row.issuedAt).format('DD.MM.YYYY HH:mm')}
                </Typography.Text>
            </Space>
        ),
    },
    {
        title: 'Kasse (FK)',
        dataIndex: 'cashRegisterId',
        key: 'cashRegisterFk',
        width: 160,
        ellipsis: true,
        render: (_: string, row) =>
            row.cashRegisterId ? (
                <Typography.Text type="secondary" style={{ fontFamily: 'monospace', fontSize: 11 }} ellipsis>
                    {row.cashRegisterId}
                </Typography.Text>
            ) : (
                <Typography.Text type="secondary">—</Typography.Text>
            ),
    },
    {
        title: 'Kassen-ID (Anzeige)',
        dataIndex: 'registerDisplayNumber',
        key: 'registerDisplayNumber',
        width: 120,
        render: (_: string | undefined, row) => {
            const disp = formatRegisterDisplayLabel(row.registerDisplayNumber);
            return disp === '—' ? (
                <Typography.Text type="secondary">—</Typography.Text>
            ) : (
                <Tag>{disp}</Tag>
            );
        },
    },
    {
        title: 'Kassierer (Id)',
        dataIndex: 'cashierId',
        key: 'cashierId',
        width: 120,
        ellipsis: true,
        render: (text: string | null) => (
            <Typography.Text type="secondary" ellipsis>
                {text || '—'}
            </Typography.Text>
        ),
    },
    {
        title: 'Grand Total',
        dataIndex: 'grandTotal',
        key: 'grandTotal',
        align: 'right',
        width: 120,
        sorter: true,
        render: (val: number) => (
            <Typography.Text strong style={{ fontVariantNumeric: 'tabular-nums' }}>
                {formatEUR(val)}
            </Typography.Text>
        ),
    },
    {
        title: 'Next',
        key: 'actions',
        width: 108,
        fixed: 'right',
        align: 'right' as const,
        render: (_: unknown, record: ReceiptListItemDto) => (
            <Link href={`/receipts/${record.receiptId}`}>
                <Button type="primary" size="small" icon={<EyeOutlined />}>
                    Open
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
    emptyText,
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
                hideOnSinglePage: false,
            }}
            scroll={{ x: 1100 }}
            locale={emptyText ? { emptyText } : undefined}
            style={{
                opacity: isPlaceholderData ? 0.6 : 1,
                transition: 'opacity 0.2s',
            }}
            size="middle"
        />
    );
}
