'use client';

import React, { useMemo } from 'react';
import { Statistic, Row, Col, Typography } from 'antd';
import dayjs from 'dayjs';
import { formatUserMonthDay } from '@/lib/dateFormatter';
import {
    LineChart,
    Line,
    XAxis,
    YAxis,
    Tooltip as RechartsTooltip,
    ResponsiveContainer,
} from 'recharts';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useGetApiReportsSales } from '@/api/generated/reports/reports';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import { PERMISSIONS } from '@/shared/auth/permissions';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'>;

export function TodaySalesWidget({ title, dragHandleProps, onRefresh }: Props) {
    const today = dayjs().format('YYYY-MM-DD');
    const { isAuthorized } = useAuthorizationGate({ requiredPermission: PERMISSIONS.REPORT_VIEW });
    const query = useGetApiReportsSales(
        { startDate: today, endDate: today },
        {
            query: {
                enabled: isAuthorized,
                refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
                staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
            },
        },
    );

    const chartData = useMemo(
        () =>
            (query.data?.dailySales ?? []).map((d) => ({
                date: d.date ? formatUserMonthDay(d.date) || '—' : '—',
                total: d.total ?? 0,
            })),
        [query.data?.dailySales],
    );

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
        >
            <Row gutter={16}>
                <Col xs={24} sm={12}>
                    <Statistic
                        title="Gesamtumsatz heute"
                        value={query.data?.totalSales ?? 0}
                        precision={2}
                        suffix="€"
                        loading={query.isLoading}
                    />
                    <Typography.Text type="secondary">
                        {query.data?.totalInvoices ?? 0} Verkäufe
                    </Typography.Text>
                </Col>
                <Col xs={24} sm={12} style={{ minHeight: 120 }}>
                    {chartData.length > 0 ? (
                        <ResponsiveContainer width="100%" height={120}>
                            <LineChart data={chartData}>
                                <XAxis dataKey="date" hide />
                                <YAxis hide />
                                <RechartsTooltip formatter={(v: number) => `€${v.toFixed(2)}`} />
                                <Line type="monotone" dataKey="total" stroke="#1677ff" dot={false} />
                            </LineChart>
                        </ResponsiveContainer>
                    ) : (
                        <Typography.Text type="secondary">Keine Verkäufe heute</Typography.Text>
                    )}
                </Col>
            </Row>
        </WidgetShell>
    );
}
