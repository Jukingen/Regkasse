'use client';

import React, { useMemo } from 'react';
import { Statistic, Row, Col, Typography } from 'antd';
import dayjs from 'dayjs';
import {
    LineChart,
    Line,
    XAxis,
    YAxis,
    Tooltip as RechartsTooltip,
    ResponsiveContainer,
} from 'recharts';
import { useGetApiReportsSales } from '@/api/generated/reports/reports';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'>;

export function TodaySalesWidget({ title, dragHandleProps, onRefresh }: Props) {
    const today = dayjs().format('YYYY-MM-DD');
    const query = useGetApiReportsSales(
        { startDate: today, endDate: today },
        {
            query: {
                refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
                staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
            },
        },
    );

    const chartData = useMemo(
        () =>
            (query.data?.dailySales ?? []).map((d) => ({
                date: d.date ? dayjs(d.date).format('DD.MM.') : '—',
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
