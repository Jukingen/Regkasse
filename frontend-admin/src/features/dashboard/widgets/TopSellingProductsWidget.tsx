'use client';

import React, { useMemo } from 'react';
import { Segmented, Table } from 'antd';
import dayjs from 'dayjs';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useGetApiReportsProducts } from '@/api/generated/reports/reports';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import { PERMISSIONS } from '@/shared/auth/permissions';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';

type Period = 'today' | 'week';

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'> & {
    period: Period;
    onPeriodChange: (period: Period) => void;
};

function rangeForPeriod(period: Period): { startDate: string; endDate: string } {
    const end = dayjs();
    if (period === 'week') {
        return {
            startDate: end.startOf('isoWeek').format('YYYY-MM-DD'),
            endDate: end.format('YYYY-MM-DD'),
        };
    }
    const d = end.format('YYYY-MM-DD');
    return { startDate: d, endDate: d };
}

export function TopSellingProductsWidget({
    title,
    dragHandleProps,
    onRefresh,
    period,
    onPeriodChange,
}: Props) {
    const range = useMemo(() => rangeForPeriod(period), [period]);
    const { isAuthorized } = useAuthorizationGate({ requiredPermission: PERMISSIONS.REPORT_VIEW });
    const query = useGetApiReportsProducts(range, {
        query: {
            enabled: isAuthorized,
            refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
            staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
        },
    });

    const rows = (query.data?.topSellingProducts ?? []).slice(0, 5);

    const handleRefresh = () => {
        void query.refetch();
        onRefresh?.();
    };

    return (
        <WidgetShell
            title={title}
            dragHandleProps={dragHandleProps}
            onRefresh={handleRefresh}
            refreshing={query.isFetching}
            extra={
                <Segmented
                    size="small"
                    value={period}
                    options={[
                        { label: 'Heute', value: 'today' },
                        { label: 'Woche', value: 'week' },
                    ]}
                    onChange={(v) => onPeriodChange(v as Period)}
                />
            }
        >
            <Table
                size="small"
                pagination={false}
                loading={query.isLoading}
                dataSource={rows}
                rowKey="productId"
                columns={[
                    { title: 'Produkt', dataIndex: 'productName' },
                    { title: 'Menge', dataIndex: 'quantitySold', width: 80 },
                    {
                        title: 'Umsatz',
                        dataIndex: 'revenue',
                        width: 100,
                        render: (val: number) => `€${Number(val ?? 0).toFixed(2)}`,
                    },
                ]}
            />
        </WidgetShell>
    );
}
