'use client';

import React, { Suspense } from 'react';
import { Card, Typography, Spin } from 'antd';
import type { TablePaginationConfig } from 'antd/es/table';
import type { SorterResult } from 'antd/es/table/interface';
import { useReceiptSearchParams } from '@/features/receipts/hooks/useReceiptSearchParams';
import { useReceiptListQuery } from '@/features/receipts/hooks/useReceiptListQuery';
import ReceiptsFilterBar from '@/features/receipts/components/ReceiptsFilterBar';
import ReceiptsTable from '@/features/receipts/components/ReceiptsTable';
import type { ReceiptListItemDto, ReceiptListParams } from '@/features/receipts/types/receipts';

const { Title } = Typography;

function ReceiptsPageContent() {
    const { params, setParams, resetFilters } = useReceiptSearchParams();
    const { data, isLoading, isPlaceholderData } = useReceiptListQuery(params);

    /** Parse sort string "field:order" into Ant Design's sortField/sortOrder */
    const parsedSort = params.sort?.split(':') ?? [];
    const sortField = parsedSort[0];
    const sortOrder = parsedSort[1] === 'asc' ? 'ascend' as const : 'descend' as const;

    /** Handle Ant Table onChange for pagination + sorting */
    const handleTableChange = (
        pagination: TablePaginationConfig,
        _filters: Record<string, any>,
        sorter: SorterResult<ReceiptListItemDto> | SorterResult<ReceiptListItemDto>[],
    ) => {
        const single = Array.isArray(sorter) ? sorter[0] : sorter;
        const updates: Partial<ReceiptListParams> = {
            page: pagination.current ?? 1,
            pageSize: pagination.pageSize ?? 25,
        };

        if (single?.field && single?.order) {
            const dir = single.order === 'ascend' ? 'asc' : 'desc';
            updates.sort = `${String(single.field)}:${dir}`;
        }

        setParams(updates);
    };

    /** Handle filter form submission */
    const handleFilterChange = (values: Partial<ReceiptListParams>) => {
        setParams(values);
    };

    return (
        <Card>
            <Title level={3} style={{ marginBottom: 16 }}>Receipts</Title>

            <ReceiptsFilterBar
                initialValues={params}
                onFilterChange={handleFilterChange}
                onReset={resetFilters}
                loading={isLoading}
            />

            <ReceiptsTable
                data={data?.items ?? []}
                loading={isLoading}
                isPlaceholderData={isPlaceholderData}
                pagination={{
                    current: data?.page ?? params.page,
                    pageSize: data?.pageSize ?? params.pageSize,
                    total: data?.totalCount ?? 0,
                }}
                sortField={sortField}
                sortOrder={sortOrder}
                onTableChange={handleTableChange}
            />
        </Card>
    );
}

/**
 * Receipts List Page
 * Wrapped in Suspense because useSearchParams() requires it in Next.js App Router.
 */
export default function ReceiptsPage() {
    return (
        <Suspense fallback={<Spin style={{ display: 'block', margin: '80px auto' }} />}>
            <ReceiptsPageContent />
        </Suspense>
    );
}
