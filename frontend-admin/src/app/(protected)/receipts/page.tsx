'use client';

import React, { Suspense, useMemo } from 'react';
import { Space, Spin, Alert, Button, Tooltip, Typography } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import type { TablePaginationConfig } from 'antd/es/table';
import type { FilterValue, SorterResult } from 'antd/es/table/interface';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_NAV_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
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
    return 'Belege konnten nicht geladen werden. Bitte erneut versuchen.';
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
    const emptyText = isEmpty ? 'Keine Belege für diese Filter. Filter anpassen oder Zeitraum erweitern.' : undefined;

    const scopeSummary = useMemo(() => {
        const p = data?.page ?? params.page;
        const ps = data?.pageSize ?? params.pageSize;
        const tc = data?.totalCount;
        const parts = [
            `Seite ${p} · ${ps} pro Seite`,
            tc != null ? `${tc.toLocaleString('de-DE')} Belege (API)` : 'Gesamtanzahl nicht geladen',
        ];
        if (params.receiptNumber?.trim()) {
            parts.push(`Belegnr. «${params.receiptNumber.trim()}»`);
        }
        if (params.cashRegisterId?.trim()) {
            parts.push(`Kasse ${params.cashRegisterId.trim()}`);
        }
        if (params.cashierId?.trim()) {
            parts.push(`Kassierer ${params.cashierId.trim()}`);
        }
        if (params.issuedFrom && params.issuedTo) {
            parts.push(
                `${dayjs(params.issuedFrom).format('DD.MM.YYYY')}–${dayjs(params.issuedTo).format('DD.MM.YYYY')}`,
            );
        } else {
            parts.push('kein Datumsfilter');
        }
        if (params.sort) {
            parts.push(`Sortierung ${params.sort}`);
        }
        return parts.join(' · ');
    }, [data?.page, data?.pageSize, data?.totalCount, params]);

    return (
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
            <AdminPageHeader
                title={ADMIN_NAV_LABELS.receipts}
                breadcrumbs={[ADMIN_OVERVIEW_CRUMB, { title: ADMIN_NAV_LABELS.receipts }]}
                actions={
                    <Tooltip title="Daten vom Server neu laden (Liste kann gecacht sein).">
                        <Button
                            icon={<ReloadOutlined />}
                            onClick={() => refetch()}
                            loading={isLoading}
                        >
                            Aktualisieren
                        </Button>
                    </Tooltip>
                }
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    Serverseitig gefilterte Belegliste. Mit den Filtern eingrenzen, anschließend die Tabelle prüfen.
                </Typography.Paragraph>
            </AdminPageHeader>

            {isError ? (
                <Alert
                    type="error"
                    message="Belege konnten nicht geladen werden"
                    description={getReceiptListErrorMessage(error)}
                    showIcon
                    action={
                        <Button size="small" onClick={() => refetch()}>
                            Erneut versuchen
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
                        Aktive Ansicht:{' '}
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
                    <Spin size="large" tip="Belege werden geladen…" />
                </div>
            }
        >
            <ReceiptsPageContent />
        </Suspense>
    );
}
