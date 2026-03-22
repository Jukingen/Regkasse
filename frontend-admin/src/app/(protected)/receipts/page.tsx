'use client';

import React, { Suspense, useMemo } from 'react';
import { Space, Spin, Alert, Button, Tooltip, Typography } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import type { TablePaginationConfig } from 'antd/es/table';
import type { FilterValue, SorterResult } from 'antd/es/table/interface';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useReceiptSearchParams } from '@/features/receipts/hooks/useReceiptSearchParams';
import { useReceiptListQuery } from '@/features/receipts/hooks/useReceiptListQuery';
import ReceiptsFilterBar from '@/features/receipts/components/ReceiptsFilterBar';
import ReceiptsTable from '@/features/receipts/components/ReceiptsTable';
import type { ReceiptListItemDto, ReceiptListParams } from '@/features/receipts/types/receipts';

/** Extract user-facing error message from API/network error */
function getReceiptListErrorMessage(error: unknown): string {
    if (error instanceof Error) return error.message;
    const norm = (error as { normalized?: { message?: string } })?.normalized;
    if (norm?.message) return norm.message;
    return 'Failed to load receipts. Please try again.';
}

function ReceiptsPageContent() {
    const { params, setParams, resetFilters } = useReceiptSearchParams();
    const { data, isLoading, isPlaceholderData, isError, error, refetch } = useReceiptListQuery(params);

    /** Parse sort string "field:order" into Ant Design's sortField/sortOrder */
    const parsedSort = params.sort?.split(':') ?? [];
    const sortField = parsedSort[0];
    const sortOrder = parsedSort[1] === 'asc' ? 'ascend' as const : 'descend' as const;

    /** Handle Ant Table onChange for pagination + sorting */
    const handleTableChange = (
        pagination: TablePaginationConfig,
        _filters: Record<string, FilterValue | null>,
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

    const isEmpty = !isLoading && !isError && (!data?.items?.length);
    const emptyText = isEmpty ? 'No receipts found. Try adjusting filters.' : undefined;

    const scopeSummary = useMemo(() => {
        const p = data?.page ?? params.page;
        const ps = data?.pageSize ?? params.pageSize;
        const tc = data?.totalCount;
        const parts = [
            `Page ${p} · ${ps} per page`,
            tc != null ? `${tc.toLocaleString()} receipts (API)` : 'total not loaded',
        ];
        if (params.receiptNumber?.trim()) {
            parts.push(`receipt # «${params.receiptNumber.trim()}»`);
        }
        if (params.cashRegisterId?.trim()) {
            parts.push(`register ${params.cashRegisterId.trim()}`);
        }
        if (params.cashierId?.trim()) {
            parts.push(`cashier ${params.cashierId.trim()}`);
        }
        if (params.issuedFrom && params.issuedTo) {
            parts.push(
                `${dayjs(params.issuedFrom).format('DD.MM.YYYY')}–${dayjs(params.issuedTo).format('DD.MM.YYYY')}`,
            );
        } else {
            parts.push('no date range');
        }
        if (params.sort) {
            parts.push(`sort ${params.sort}`);
        }
        return parts.join(' · ');
    }, [data?.page, data?.pageSize, data?.totalCount, params]);

    return (
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
            <AdminPageHeader
                title="Receipts"
                breadcrumbs={[
                    { title: 'Dashboard', href: '/dashboard' },
                    { title: 'Receipts' },
                ]}
                actions={
                    <Tooltip title="Reload data from server (list may be cached).">
                        <Button
                            icon={<ReloadOutlined />}
                            onClick={() => refetch()}
                            loading={isLoading}
                        >
                            Refresh
                        </Button>
                    </Tooltip>
                }
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    Server-filtered receipt list. Narrow results with the filters below, then review rows in the table.
                </Typography.Paragraph>
            </AdminPageHeader>

            {isError ? (
                <Alert
                    type="error"
                    message="Failed to load receipts"
                    description={getReceiptListErrorMessage(error)}
                    showIcon
                    action={
                        <Button size="small" onClick={() => refetch()}>
                            Try again
                        </Button>
                    }
                />
            ) : null}

            <ReceiptsFilterBar
                initialValues={params}
                onFilterChange={handleFilterChange}
                onReset={resetFilters}
                loading={isLoading}
            />

            {!isError ? (
                <Typography.Paragraph
                    type="secondary"
                    style={{
                        marginBottom: 0,
                        marginTop: 8,
                        fontSize: 12,
                        padding: '8px 12px',
                        background: 'var(--ant-color-fill-quaternary)',
                        borderRadius: 6,
                    }}
                >
                    <Typography.Text strong style={{ fontSize: 12 }}>
                        Active scope:{' '}
                    </Typography.Text>
                    {scopeSummary}
                </Typography.Paragraph>
            ) : null}

            {!isError ? (
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
                    emptyText={emptyText}
                />
            ) : null}
        </Space>
    );
}

/**
 * Receipts List Page
 * Wrapped in Suspense because useSearchParams() requires it in Next.js App Router.
 */
export default function ReceiptsPage() {
    return (
        <Suspense
            fallback={
                <div style={{ padding: 80, textAlign: 'center' }}>
                    <Spin size="large" tip="Loading receipts…" />
                </div>
            }
        >
            <ReceiptsPageContent />
        </Suspense>
    );
}
