'use client';

import React, { useMemo } from 'react';
import { Col, Row, Segmented, Statistic, Tag, Typography } from 'antd';
import {
    ArrowDownOutlined,
    ArrowUpOutlined,
    MinusOutlined,
} from '@ant-design/icons';
import {
    Bar,
    BarChart,
    CartesianGrid,
    Line,
    LineChart,
    ResponsiveContainer,
    Tooltip,
    XAxis,
    YAxis,
} from 'recharts';

import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import { usePaymentTrends } from '@/features/payments/hooks/usePaymentTrends';
import type { TrendPeriod } from '@/features/payments/types/paymentTrends';
import { useI18n } from '@/i18n/I18nProvider';
import { formatUserMonthDay } from '@/lib/dateFormatter';

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'> & {
    period?: TrendPeriod;
    onPeriodChange?: (period: TrendPeriod) => void;
};

const PERIOD_OPTIONS: TrendPeriod[] = ['Daily', 'Weekly', 'Monthly'];

function parsePeriod(value: unknown): TrendPeriod {
    if (value === 'Weekly' || value === 'Monthly') return value;
    return 'Daily';
}

export function PaymentTrendWidget({
    title,
    dragHandleProps,
    onRefresh,
    period: periodProp,
    onPeriodChange,
}: Props) {
    const { t } = useI18n();
    const [localPeriod, setLocalPeriod] = React.useState<TrendPeriod>('Daily');
    const period = periodProp ?? localPeriod;

    const query = usePaymentTrends(period, null, true);

    const chartData = useMemo(
        () =>
            (query.data?.trendData ?? []).map((point) => ({
                label:
                    point.label ??
                    formatUserMonthDay(point.date),
                revenue: point.totalAmount,
                count: point.transactionCount,
            })),
        [query.data?.trendData],
    );

    const comparison = query.data?.comparison;
    const summary = query.data?.summary;

    const handlePeriodChange = (value: string | number) => {
        const next = parsePeriod(value);
        setLocalPeriod(next);
        onPeriodChange?.(next);
    };

    const handleRefresh = () => {
        void query.refetch();
        onRefresh?.();
    };

    const trendTag = (() => {
        if (!comparison) return null;
        if (comparison.trend === 'up') {
            return (
                <Tag color="green" icon={<ArrowUpOutlined />}>
                    {comparison.growthPercentage.toFixed(1)}%
                </Tag>
            );
        }
        if (comparison.trend === 'down') {
            return (
                <Tag color="red" icon={<ArrowDownOutlined />}>
                    {comparison.growthPercentage.toFixed(1)}%
                </Tag>
            );
        }
        return (
            <Tag icon={<MinusOutlined />}>
                {comparison.growthPercentage.toFixed(1)}%
            </Tag>
        );
    })();

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
                    options={PERIOD_OPTIONS.map((p) => ({
                        label: t(`payments.trends.period.${p}`),
                        value: p,
                    }))}
                    onChange={handlePeriodChange}
                />
            }
        >
            <Row gutter={[16, 16]}>
                <Col xs={24} sm={8}>
                    <Statistic
                        title={t('payments.trends.totalRevenue')}
                        value={summary?.totalRevenue ?? 0}
                        precision={2}
                        suffix="€"
                        loading={query.isLoading}
                    />
                    <Typography.Text type="secondary">
                        {t('payments.trends.transactions', {
                            count: summary?.totalTransactions ?? 0,
                        })}
                    </Typography.Text>
                </Col>
                <Col xs={24} sm={8}>
                    <Statistic
                        title={t('payments.trends.avgTransaction')}
                        value={summary?.averageTransactionValue ?? 0}
                        precision={2}
                        suffix="€"
                        loading={query.isLoading}
                    />
                </Col>
                <Col xs={24} sm={8}>
                    <Statistic
                        title={t('payments.trends.periodComparison')}
                        value={comparison?.currentPeriodTotal ?? 0}
                        precision={2}
                        suffix="€"
                        loading={query.isLoading}
                    />
                    {trendTag}
                </Col>
            </Row>

            <div style={{ marginTop: 16, minHeight: 220 }}>
                {chartData.length > 0 ? (
                    <ResponsiveContainer width="100%" height={220}>
                        {period === 'Daily' ? (
                            <LineChart data={chartData}>
                                <CartesianGrid strokeDasharray="3 3" stroke="rgba(0,0,0,0.06)" />
                                <XAxis dataKey="label" tick={{ fontSize: 11 }} interval="preserveStartEnd" />
                                <YAxis tick={{ fontSize: 11 }} width={48} />
                                <Tooltip
                                    formatter={(value: number, name: string) =>
                                        name === 'revenue'
                                            ? [`€${value.toFixed(2)}`, t('payments.trends.chart.revenue')]
                                            : [value, t('payments.trends.chart.count')]
                                    }
                                />
                                <Line
                                    type="monotone"
                                    dataKey="revenue"
                                    stroke="#1677ff"
                                    strokeWidth={2}
                                    dot={false}
                                />
                            </LineChart>
                        ) : (
                            <BarChart data={chartData}>
                                <CartesianGrid strokeDasharray="3 3" stroke="rgba(0,0,0,0.06)" />
                                <XAxis dataKey="label" tick={{ fontSize: 11 }} interval="preserveStartEnd" />
                                <YAxis tick={{ fontSize: 11 }} width={48} />
                                <Tooltip
                                    formatter={(value: number) => [
                                        `€${value.toFixed(2)}`,
                                        t('payments.trends.chart.revenue'),
                                    ]}
                                />
                                <Bar dataKey="revenue" fill="#1677ff" radius={[4, 4, 0, 0]} maxBarSize={40} />
                            </BarChart>
                        )}
                    </ResponsiveContainer>
                ) : (
                    <Typography.Text type="secondary">{t('payments.trends.empty')}</Typography.Text>
                )}
            </div>

            {summary?.mostUsedPaymentMethod ? (
                <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8 }}>
                    {t('payments.trends.insights', {
                        method: summary.mostUsedPaymentMethod,
                        hour: summary.peakHour,
                        bestDay: summary.bestDay ?? '—',
                    })}
                </Typography.Text>
            ) : null}
        </WidgetShell>
    );
}

export { parsePeriod as parsePaymentTrendPeriod };
